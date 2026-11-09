using LegalAiLab.Core;

namespace LegalAiLab.Retrieval;

public sealed class Bm25Scorer
{
    private const double K1 = 1.2;
    private const double B = 0.75;

    public IEnumerable<(double Score, TextChunk Chunk, JudgmentRecord Record)> Score(string query, IReadOnlyList<IndexedChunk> rows)
    {
        var tokenizer = new TextTokenizer();
        var queryTokens = tokenizer.Tokenize(query).Distinct().ToList();
        if (rows.Count == 0 || queryTokens.Count == 0)
        {
            yield break;
        }

        var docs = rows.Select(row => tokenizer.Tokenize(row.Chunk.Text).ToList()).ToList();
        var avg = docs.Average(doc => doc.Count);
        var df = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in queryTokens)
        {
            df[token] = docs.Count(doc => doc.Contains(token));
        }

        for (var i = 0; i < rows.Count; i++)
        {
            double total = 0;
            var doc = docs[i];
            foreach (var token in queryTokens)
            {
                var tf = doc.Count(word => word == token);
                if (tf == 0)
                {
                    continue;
                }

                var idf = Math.Log(1 + (rows.Count - df[token] + 0.5) / (df[token] + 0.5));
                var denom = tf + K1 * (1 - B + B * doc.Count / Math.Max(1, avg));
                total += idf * (tf * (K1 + 1)) / denom;
            }

            yield return (total, rows[i].Chunk, rows[i].Record);
        }
    }
}
