using LegalAiLab.Core;
using LegalAiLab.Mcp;

namespace LegalAiLab.Workflows;

public sealed class QueryDecomposer
{
    public IReadOnlyList<string> Decompose(string question)
    {
        var parts = new List<string> { question };
        AddPrivacyQueries(question, parts);
        AddSpeechQuery(question, parts);
        return parts;
    }

    private static void AddPrivacyQueries(string question, List<string> parts)
    {
        if (!question.Contains("privacy", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        parts.Add("Kharak Singh surveillance personal liberty");
        parts.Add("Puttaswamy privacy");
    }

    private static void AddSpeechQuery(string question, List<string> parts)
    {
        var mentionsSpeech = question.Contains("speech", StringComparison.OrdinalIgnoreCase);
        var mentionsArticle19 = question.Contains("article 19", StringComparison.OrdinalIgnoreCase);
        if (mentionsSpeech || mentionsArticle19)
        {
            parts.Add("Romesh Thappar freedom of speech Madras");
        }
    }
}

public sealed class CaseDiscovery
{
    private readonly LegalResearchLibrary _library;
    private readonly QueryDecomposer _decomposer = new();

    public CaseDiscovery(LegalResearchLibrary library) => _library = library;

    public List<RetrievedPassage> Discover(string question)
    {
        var found = new List<RetrievedPassage>();
        foreach (var query in _decomposer.Decompose(question))
        {
            var result = _library.SearchCases(query, "hybrid", 3);
            if (result.Ok)
            {
                found.AddRange(result.Value!);
            }
        }

        return found
            .GroupBy(passage => passage.ChunkId)
            .Select(group => group.OrderByDescending(passage => passage.Score).First())
            .Take(6)
            .ToList();
    }
}

public sealed class JudgmentAnalysis
{
    public async Task<List<string>> AnalyzeAsync(IReadOnlyList<RetrievedPassage> passages)
    {
        var groups = passages.GroupBy(passage => passage.CaseId).Take(3).ToList();
        var notes = await Task.WhenAll(groups.Select(group => Task.Run(() => Describe(group))));
        return notes.ToList();
    }

    private static string Describe(IGrouping<string, RetrievedPassage> group)
    {
        var first = group.First();
        return $"{group.Key} ({first.Citation}) has {group.Count()} retrieved passage(s). Synthetic={first.IsSynthetic}.";
    }
}

public sealed class CitationVerification
{
    private readonly LegalResearchLibrary _library;

    public CitationVerification(LegalResearchLibrary library) => _library = library;

    public List<string> Verify(IReadOnlyList<RetrievedPassage> passages)
    {
        var notes = passages.Select(VerifyPassage).ToList();
        notes.AddRange(passages.Select(passage => passage.CaseId).Distinct().Select(CiteNote).OfType<string>());
        return notes;
    }

    private string VerifyPassage(RetrievedPassage passage)
    {
        var exact = _library.GetPassage(passage.ChunkId);
        return exact.Ok ? $"verified {passage.ChunkId} against stored text" : $"missing {passage.ChunkId}";
    }

    private string? CiteNote(string caseId)
    {
        var links = _library.FindCitedCases(caseId);
        if (!links.Ok || links.Value!.Count == 0)
        {
            return null;
        }

        return $"{caseId} string-cites {links.Value.Count} catalog case(s). Overruling is not inferred.";
    }
}

public sealed class ResearchReportWriter
{
    public ResearchReport Write(string question, ResearchState state)
    {
        return new ResearchReport
        {
            ReportId = Guid.NewGuid().ToString("n"),
            Question = question,
            CreatedUtc = DateTime.UtcNow.ToString("O"),
            CasesConsidered = state.Discovered.Select(passage => passage.CaseId).Distinct().ToList(),
            Findings = state.Analyses,
            Evidence = state.Discovered,
            CompetingInterpretations = ["Passages can support more than one reading. This workflow does not choose a binding interpretation."],
            Limitations =
            [
                "The corpus is a small public subset plus fictional fixtures.",
                "Metadata-only judgments have no full text.",
                "Citation links are string matches, not a holding that one case overrules another.",
                ProductBoundary.Notice
            ],
            References = state.Discovered.Select(passage => $"{passage.Citation} {passage.ChunkId}").Distinct().ToList(),
            HumanReview = "Required before any reliance."
        };
    }

    public ResearchReport Empty(string question, string reason) => new()
    {
        ReportId = Guid.NewGuid().ToString("n"),
        Question = question,
        CreatedUtc = DateTime.UtcNow.ToString("O"),
        CasesConsidered = [],
        Findings = [reason],
        Evidence = [],
        CompetingInterpretations = [],
        Limitations = [ProductBoundary.Notice],
        References = [],
        HumanReview = "Required."
    };
}
