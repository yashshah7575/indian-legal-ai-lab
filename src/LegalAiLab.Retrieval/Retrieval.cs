using LegalAiLab.Core;
using LegalAiLab.Ingestion;

namespace LegalAiLab.Retrieval;

public sealed record IndexedChunk(TextChunk Chunk, JudgmentRecord Record, float[] Vector);

public sealed class HashEmbeddingGenerator
{
    public const int Dimensions = 64;
    private readonly TextTokenizer _tokenizer = new();
    private readonly VectorMath _math = new();

    public float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        foreach (var token in _tokenizer.Tokenize(text))
        {
            vector[Bucket(token)] += 1f;
        }

        return _math.Normalize(vector);
    }

    public static IEnumerable<string> Tokenize(string text) => new TextTokenizer().Tokenize(text);

    private static int Bucket(string token)
    {
        var hash = 17;
        foreach (var ch in token)
        {
            hash = unchecked((hash * 31) + ch);
        }

        return (hash & int.MaxValue) % Dimensions;
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
    private readonly KeywordScorer _keywords = new();
    private readonly VectorMath _math = new();
    private readonly IndexedChunkFilter _filter = new();
    private readonly PassageFormatter _formatter = new();
    private readonly List<IndexedChunk> _rows = [];

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
            _rows.RemoveAll(row => row.Chunk.ChunkId == chunk.ChunkId);
            _rows.Add(new IndexedChunk(chunk, record, _embeddings.Embed(chunk.Text)));
        }
    }

    public IReadOnlyList<RetrievedPassage> Search(string query, string method, int take = 5, SearchFilters? filters = null)
    {
        var rows = _filter.Apply(_rows, filters);
        var normalized = method.ToLowerInvariant();
        var scored = Score(query, normalized, rows);
        return scored
            .Where(row => row.Score > 0)
            .OrderByDescending(row => row.Score)
            .Take(take)
            .Select(row => _formatter.ToPassage(row.Record, row.Chunk, row.Score, normalized))
            .ToList();
    }

    public string ToPgVectorLiteral(string text)
    {
        var values = _embeddings.Embed(text).Select(v => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));
        return "[" + string.Join(",", values) + "]";
    }

    public static double Keyword(string query, string text) => new KeywordScorer().Score(query, text);

    public static double Cosine(float[] left, float[] right) => new VectorMath().Cosine(left, right);

    private IEnumerable<(double Score, TextChunk Chunk, JudgmentRecord Record)> Score(string query, string method, List<IndexedChunk> rows)
    {
        return method switch
        {
            "keyword" => rows.Select(row => (_keywords.Score(query, row.Chunk.Text), row.Chunk, row.Record)),
            "bm25" => new Bm25Scorer().Score(query, rows),
            "vector" => ScoreVector(query, rows),
            "hybrid" => ScoreHybrid(query, rows),
            _ => throw new ArgumentException($"Unknown retrieval method '{method}'.")
        };
    }

    private IEnumerable<(double Score, TextChunk Chunk, JudgmentRecord Record)> ScoreVector(string query, List<IndexedChunk> rows)
    {
        var embedded = _embeddings.Embed(query);
        return rows.Select(row => (_math.Cosine(embedded, row.Vector), row.Chunk, row.Record));
    }

    private IEnumerable<(double Score, TextChunk Chunk, JudgmentRecord Record)> ScoreHybrid(string query, List<IndexedChunk> rows)
    {
        var keywordScores = rows.Select(row => _keywords.Score(query, row.Chunk.Text)).ToList();
        var max = keywordScores.DefaultIfEmpty(0).Max();
        var embedded = _embeddings.Embed(query);
        for (var i = 0; i < rows.Count; i++)
        {
            var keyword = max <= 0 ? 0 : keywordScores[i] / max;
            var vector = _math.Cosine(embedded, rows[i].Vector);
            yield return ((0.55 * keyword) + (0.45 * vector), rows[i].Chunk, rows[i].Record);
        }
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
