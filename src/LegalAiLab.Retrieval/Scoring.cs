using LegalAiLab.Core;

namespace LegalAiLab.Retrieval;

public sealed class TextTokenizer
{
    public IReadOnlyList<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new List<char>();
        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Add(ch);
                continue;
            }

            Flush(tokens, current);
        }

        Flush(tokens, current);
        return tokens;
    }

    private static void Flush(List<string> tokens, List<char> current)
    {
        if (current.Count > 2)
        {
            tokens.Add(new string(current.ToArray()));
        }

        current.Clear();
    }
}

public sealed class VectorMath
{
    public double Cosine(float[] left, float[] right)
    {
        double dot = 0;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
        }

        return dot;
    }

    public float[] Normalize(float[] vector)
    {
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
}

public sealed class KeywordScorer
{
    private readonly TextTokenizer _tokenizer = new();

    public double Score(string query, string text)
    {
        var tokens = _tokenizer.Tokenize(query).Distinct().ToList();
        if (tokens.Count == 0)
        {
            return 0;
        }

        var haystack = text.ToLowerInvariant();
        return tokens.Count(token => haystack.Contains(token, StringComparison.Ordinal));
    }
}

public sealed class PassageFormatter
{
    public string Trim(string text, int max = 420)
    {
        var flat = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= max ? flat : flat[..max] + "…";
    }

    public RetrievedPassage ToPassage(JudgmentRecord record, TextChunk chunk, double score, string method)
    {
        return new RetrievedPassage
        {
            CaseId = record.CaseId,
            Citation = record.Citation,
            Title = record.Title,
            Court = record.Court,
            Year = record.Year,
            IsSynthetic = record.IsSynthetic,
            ChunkId = chunk.ChunkId,
            Start = chunk.Start,
            End = chunk.End,
            Text = Trim(chunk.Text),
            Score = score,
            Method = method
        };
    }
}

public sealed class IndexedChunkFilter
{
    public List<IndexedChunk> Apply(IEnumerable<IndexedChunk> rows, SearchFilters? filters)
    {
        var query = rows;
        if (filters?.Court is not null)
        {
            query = query.Where(row => row.Record.Court.Contains(filters.Court, StringComparison.OrdinalIgnoreCase));
        }

        if (filters?.Year is not null)
        {
            query = query.Where(row => row.Record.Year == filters.Year);
        }

        if (filters?.CaseId is not null)
        {
            query = query.Where(row => row.Record.CaseId.Equals(filters.CaseId, StringComparison.OrdinalIgnoreCase));
        }

        if (filters?.Synthetic is not null)
        {
            query = query.Where(row => row.Record.IsSynthetic == filters.Synthetic);
        }

        return query.ToList();
    }
}
