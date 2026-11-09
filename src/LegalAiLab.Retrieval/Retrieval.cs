using LegalAiLab.Core;
using LegalAiLab.Ingestion;

namespace LegalAiLab.Retrieval;

public sealed class HashEmbeddingGenerator
{
    public const int Dimensions = 64;

    public float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        var tokens = Tokenize(text);
        foreach (var token in tokens)
        {
            var hash = 17;
            foreach (var ch in token)
            {
                hash = unchecked((hash * 31) + ch);
            }

            var index = (hash & int.MaxValue) % Dimensions;
            vector[index] += 1f;
        }

        var norm = Math.Sqrt(vector.Sum(v => v * v));
        if (norm == 0)
        {
            return vector;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= (float)norm;
        }

        return vector;
    }

    public static IEnumerable<string> Tokenize(string text)
    {
        var current = new List<char>();
        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Add(ch);
            }
            else if (current.Count > 2)
            {
                yield return new string(current.ToArray());
                current.Clear();
            }
            else
            {
                current.Clear();
            }
        }

        if (current.Count > 2)
        {
            yield return new string(current.ToArray());
        }
    }
}

public sealed class SearchFilters
{
    public string? Court { get; init; }
    public int? Year { get; init; }
    public string? CaseId { get; init; }
    public bool? Synthetic { get; init; }
}

public sealed class LegalIndex
{
    private readonly HashEmbeddingGenerator _embeddings = new();
    private readonly List<(TextChunk Chunk, JudgmentRecord Record, float[] Vector)> _rows = [];

    public static LegalIndex FromRecords(IEnumerable<JudgmentRecord> records)
    {
        var index = new LegalIndex();
        foreach (var record in records)
        {
            index.Add(record);
        }

        return index;
    }

    public void Add(JudgmentRecord record)
    {
        foreach (var chunk in TextChunker.Chunk(record))
        {
            _rows.RemoveAll(r => r.Chunk.ChunkId == chunk.ChunkId);
            _rows.Add((chunk, record, _embeddings.Embed(chunk.Text)));
        }
    }

    public IReadOnlyList<RetrievedPassage> Search(string query, string method, int take = 5, SearchFilters? filters = null)
    {
        var rows = Apply(filters);
        method = method.ToLowerInvariant();
        IEnumerable<(double Score, TextChunk Chunk, JudgmentRecord Record)> scored = method switch
        {
            "keyword" => rows.Select(r => (Keyword(query, r.Chunk.Text), r.Chunk, r.Record)),
            "vector" => ScoreVector(query, rows),
            "hybrid" => ScoreHybrid(query, rows),
            _ => throw new ArgumentException($"Unknown retrieval method '{method}'.")
        };

        return scored
            .Where(s => s.Score > 0)
            .OrderByDescending(s => s.Score)
            .Take(take)
            .Select(s => new RetrievedPassage
            {
                CaseId = s.Record.CaseId,
                Citation = s.Record.Citation,
                Title = s.Record.Title,
                Court = s.Record.Court,
                Year = s.Record.Year,
                IsSynthetic = s.Record.IsSynthetic,
                ChunkId = s.Chunk.ChunkId,
                Start = s.Chunk.Start,
                End = s.Chunk.End,
                Text = Trim(s.Chunk.Text),
                Score = s.Score,
                Method = method
            })
            .ToList();
    }

    public string ToPgVectorLiteral(string text)
    {
        var values = _embeddings.Embed(text).Select(v => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));
        return "[" + string.Join(",", values) + "]";
    }

    private List<(TextChunk Chunk, JudgmentRecord Record, float[] Vector)> Apply(SearchFilters? filters)
    {
        IEnumerable<(TextChunk Chunk, JudgmentRecord Record, float[] Vector)> query = _rows;
        if (filters?.Court is not null)
        {
            query = query.Where(r => r.Record.Court.Contains(filters.Court, StringComparison.OrdinalIgnoreCase));
        }

        if (filters?.Year is not null)
        {
            query = query.Where(r => r.Record.Year == filters.Year);
        }

        if (filters?.CaseId is not null)
        {
            query = query.Where(r => r.Record.CaseId.Equals(filters.CaseId, StringComparison.OrdinalIgnoreCase));
        }

        if (filters?.Synthetic is not null)
        {
            query = query.Where(r => r.Record.IsSynthetic == filters.Synthetic);
        }

        return query.ToList();
    }

    private IEnumerable<(double Score, TextChunk Chunk, JudgmentRecord Record)> ScoreVector(string query, List<(TextChunk Chunk, JudgmentRecord Record, float[] Vector)> rows)
    {
        var q = _embeddings.Embed(query);
        return rows.Select(r => (Cosine(q, r.Vector), r.Chunk, r.Record));
    }

    private IEnumerable<(double Score, TextChunk Chunk, JudgmentRecord Record)> ScoreHybrid(string query, List<(TextChunk Chunk, JudgmentRecord Record, float[] Vector)> rows)
    {
        var keywordScores = rows.Select(r => Keyword(query, r.Chunk.Text)).ToList();
        var max = keywordScores.DefaultIfEmpty(0).Max();
        var q = _embeddings.Embed(query);
        for (var i = 0; i < rows.Count; i++)
        {
            var keyword = max <= 0 ? 0 : keywordScores[i] / max;
            var vector = Cosine(q, rows[i].Vector);
            yield return ((0.55 * keyword) + (0.45 * vector), rows[i].Chunk, rows[i].Record);
        }
    }

    public static double Keyword(string query, string text)
    {
        var tokens = HashEmbeddingGenerator.Tokenize(query).Distinct().ToList();
        if (tokens.Count == 0)
        {
            return 0;
        }

        var hay = text.ToLowerInvariant();
        return tokens.Count(t => hay.Contains(t, StringComparison.Ordinal));
    }

    public static double Cosine(float[] a, float[] b)
    {
        double dot = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
        }

        return dot;
    }

    private static string Trim(string text)
    {
        var flat = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= 420 ? flat : flat[..420] + "…";
    }
}

public static class PgVectorSql
{
    public const string InitScript = """
        CREATE EXTENSION IF NOT EXISTS vector;
        CREATE TABLE IF NOT EXISTS judgment_chunks (
          chunk_id text PRIMARY KEY,
          case_id text NOT NULL,
          court text NOT NULL,
          year int NOT NULL,
          content text NOT NULL,
          embedding vector(64) NOT NULL
        );
        """;

    public static string SearchSql { get; } = """
        SELECT chunk_id, case_id, content, 1 - (embedding <=> @embedding::vector) AS score
        FROM judgment_chunks
        WHERE (@court::text IS NULL OR court ILIKE @court)
          AND (@year::int IS NULL OR year = @year)
        ORDER BY embedding <=> @embedding::vector
        LIMIT @take;
        """;
}
