using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LegalAiLab.Core;

namespace LegalAiLab.Ingestion;

public static class TextChunker
{
    public static IReadOnlyList<TextChunk> Chunk(JudgmentRecord record, int size = 900, int overlap = 120)
    {
        if (string.IsNullOrWhiteSpace(record.Text))
        {
            return [];
        }

        var text = record.Text.Replace("\r\n", "\n");
        var chunks = new List<TextChunk>();
        var start = 0;
        var index = 0;
        while (start < text.Length)
        {
            var len = Math.Min(size, text.Length - start);
            var slice = text.Substring(start, len);
            chunks.Add(new TextChunk
            {
                ChunkId = $"{record.CaseId}:{index}",
                CaseId = record.CaseId,
                Index = index,
                Text = slice,
                Start = start,
                End = start + len
            });
            if (start + len >= text.Length)
            {
                break;
            }

            start += Math.Max(1, size - overlap);
            index++;
        }

        return chunks;
    }
}

public static class DuplicateDetector
{
    public static string Fingerprint(JudgmentRecord record)
    {
        var basis = $"{record.NeutralCitation}|{record.Citation}|{record.DecisionDate}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(basis)));
    }

    public static IReadOnlyList<string> DuplicateCaseIds(IEnumerable<JudgmentRecord> records)
    {
        return records
            .GroupBy(Fingerprint)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(r => r.CaseId))
            .ToList();
    }
}

public static class CitationGraphBuilder
{
    private static readonly Regex Fictional = new(@"FICTIONAL\s+\d{4}\s+SYN\s+\d+", RegexOptions.Compiled);
    private static readonly Regex Neutral = new(@"\b(?:19|20)\d{2}\s*INSC\s*\d+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<CitationLink> Build(IEnumerable<JudgmentRecord> records)
    {
        var list = records.ToList();
        var byCitation = new Dictionary<string, JudgmentRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in list)
        {
            byCitation.TryAdd(record.Citation, record);
            byCitation.TryAdd(record.NeutralCitation, record);
            byCitation.TryAdd(record.CaseId, record);
        }

        var links = new List<CitationLink>();
        foreach (var record in list)
        {
            if (string.IsNullOrWhiteSpace(record.Text))
            {
                continue;
            }

            foreach (Match match in Fictional.Matches(record.Text).Cast<Match>().Concat(Neutral.Matches(record.Text).Cast<Match>()))
            {
                var key = Regex.Replace(match.Value, @"\s+", " ").Trim();
                if (!byCitation.TryGetValue(key, out var target) || target.CaseId == record.CaseId)
                {
                    continue;
                }

                links.Add(new CitationLink
                {
                    FromCaseId = record.CaseId,
                    ToCaseId = target.CaseId,
                    MatchedText = key,
                    Note = "String match only. This does not mean the later case overrules the earlier case."
                });
            }
        }

        return links;
    }
}

public sealed record IngestionJob(string JobId, JudgmentRecord Record, int FailTimes);

public sealed record IngestionOutcome(string JobId, string Status, int Attempts, string? Error);

public sealed class IngestionPipeline
{
    private readonly ConcurrentDictionary<string, IngestionOutcome> _completed = new();
    private readonly ConcurrentQueue<IngestionJob> _queue = new();
    private readonly ConcurrentQueue<IngestionJob> _dead = new();
    private readonly List<TextChunk> _chunks = [];
    private readonly object _gate = new();

    public IReadOnlyList<TextChunk> Chunks
    {
        get { lock (_gate) return _chunks.ToList(); }
    }

    public IReadOnlyList<IngestionJob> DeadLetters => _dead.ToList();

    public void Enqueue(IngestionJob job) => _queue.Enqueue(job);

    public IngestionOutcome ProcessNext(int maxAttempts = 3)
    {
        if (!_queue.TryDequeue(out var job))
        {
            throw new InvalidOperationException("The ingestion queue is empty.");
        }

        if (_completed.TryGetValue(job.JobId, out var existing))
        {
            return existing with { Status = "idempotent-skip" };
        }

        Exception? last = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (attempt <= job.FailTimes)
                {
                    throw new IOException("Simulated extraction failure.");
                }

                if (string.IsNullOrWhiteSpace(job.Record.Text))
                {
                    var incomplete = new IngestionOutcome(job.JobId, "incomplete-document", attempt, job.Record.ExtractionStatus);
                    _completed[job.JobId] = incomplete;
                    return incomplete;
                }

                var chunks = TextChunker.Chunk(job.Record);
                lock (_gate)
                {
                    _chunks.RemoveAll(c => c.CaseId == job.Record.CaseId);
                    _chunks.AddRange(chunks);
                }

                var done = new IngestionOutcome(job.JobId, "completed", attempt, null);
                _completed[job.JobId] = done;
                return done;
            }
            catch (Exception ex) when (ex is IOException)
            {
                last = ex;
            }
        }

        _dead.Enqueue(job);
        return new IngestionOutcome(job.JobId, "dead-letter", maxAttempts, last?.Message);
    }
}

public static class MetadataNormalizer
{
    public static JudgmentRecord Normalize(JudgmentRecord record)
    {
        var citation = Regex.Replace(record.Citation.Trim(), @"\s+", " ");
        return record with { Citation = citation, Court = record.Court.Trim() };
    }
}

public sealed class PublicBucketBootstrap
{
    private readonly HttpClient _http;

    public PublicBucketBootstrap(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public const string BucketBase = "https://indian-supreme-court-judgments.s3.amazonaws.com/";

    public async Task<BootstrapFetch> DownloadMetadataAsync(int year, string fileName, CancellationToken cancellationToken = default)
    {
        if (year < 1950 || year > 2100 || fileName.Contains("..", StringComparison.Ordinal) || !fileName.EndsWith(".json", StringComparison.Ordinal))
        {
            return new BootstrapFetch(false, null, "Refusing an unexpected metadata path.");
        }

        var url = $"{BucketBase}metadata/json/year={year}/{fileName}";
        try
        {
            var bytes = await _http.GetByteArrayAsync(url, cancellationToken);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            return new BootstrapFetch(true, hash, $"Downloaded {bytes.Length} bytes from the public bucket.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new BootstrapFetch(false, null, ex.Message);
        }
    }
}

public sealed record BootstrapFetch(bool Ok, string? Sha256, string Detail);

public static class ProvenanceWriter
{
    public static async Task WriteAsync(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}
