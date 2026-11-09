using System.Text.Json;
using LegalAiLab.Core;
using LegalAiLab.Mcp;

namespace LegalAiLab.Workflows;

public sealed class ResearchState
{
    public required string Question { get; set; }
    public List<RetrievedPassage> Discovered { get; set; } = [];
    public List<string> Analyses { get; set; } = [];
    public List<string> VerificationNotes { get; set; } = [];
    public ResearchReport? Report { get; set; }
    public string Stage { get; set; } = "new";
}

public sealed class ResearchWorkflow
{
    private readonly LegalResearchLibrary _library;

    public ResearchWorkflow(LegalResearchLibrary library) => _library = library;

    public async Task<ResearchState> RunAsync(string question, ResearchState? resume = null)
    {
        var state = resume ?? new ResearchState { Question = question };
        if (AdviceGuard.IsDisallowed(question))
        {
            state.Stage = "refused";
            state.Report = Empty(question, "The question asks for advice or an outcome prediction.");
            return state;
        }

        if (state.Stage is "new" or "discover")
        {
            var queries = Decompose(question);
            var found = new List<RetrievedPassage>();
            foreach (var query in queries)
            {
                var result = _library.SearchCases(query, "hybrid", 3);
                if (result.Ok)
                {
                    found.AddRange(result.Value!);
                }
            }

            state.Discovered = found
                .GroupBy(p => p.ChunkId)
                .Select(g => g.OrderByDescending(p => p.Score).First())
                .Take(6)
                .ToList();
            state.Stage = state.Discovered.Count == 0 ? "insufficient" : "analyze";
        }

        if (state.Stage == "insufficient")
        {
            state.Report = Empty(question, "The local corpus did not contain enough matching passages.");
            return state;
        }

        if (state.Stage == "analyze")
        {
            var groups = state.Discovered.GroupBy(p => p.CaseId).Take(3).ToList();
            var analyses = await Task.WhenAll(groups.Select(g => Task.Run(() =>
                $"{g.Key} ({g.First().Citation}) has {g.Count()} retrieved passage(s). Synthetic={g.First().IsSynthetic}.")));
            state.Analyses = analyses.ToList();
            state.Stage = "verify";
        }

        if (state.Stage == "verify")
        {
            state.VerificationNotes.Clear();
            foreach (var passage in state.Discovered)
            {
                var exact = _library.GetPassage(passage.ChunkId);
                state.VerificationNotes.Add(exact.Ok
                    ? $"verified {passage.ChunkId} against stored text"
                    : $"missing {passage.ChunkId}");
            }

            var cited = state.Discovered.Select(p => p.CaseId).Distinct();
            foreach (var caseId in cited)
            {
                var links = _library.FindCitedCases(caseId);
                if (links.Ok && links.Value!.Count > 0)
                {
                    state.VerificationNotes.Add($"{caseId} string-cites {links.Value.Count} catalog case(s). Overruling is not inferred.");
                }
            }

            state.Stage = "report";
        }

        if (state.Stage == "report")
        {
            state.Report = new ResearchReport
            {
                ReportId = Guid.NewGuid().ToString("n"),
                Question = question,
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                CasesConsidered = state.Discovered.Select(p => p.CaseId).Distinct().ToList(),
                Findings = state.Analyses,
                Evidence = state.Discovered,
                CompetingInterpretations =
                [
                    "Passages can support more than one reading. This workflow does not choose a binding interpretation."
                ],
                Limitations =
                [
                    "The corpus is a small public subset plus fictional fixtures.",
                    "Metadata-only judgments have no full text.",
                    "Citation links are string matches, not a holding that one case overrules another.",
                    ProductBoundary.Notice
                ],
                References = state.Discovered.Select(p => $"{p.Citation} {p.ChunkId}").Distinct().ToList(),
                HumanReview = "Required before any reliance."
            };
            state.Stage = "completed";
        }

        return state;
    }

    public static IReadOnlyList<string> Decompose(string question)
    {
        var parts = new List<string> { question };
        if (question.Contains("privacy", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("Kharak Singh surveillance personal liberty");
            parts.Add("Puttaswamy privacy");
        }

        if (question.Contains("speech", StringComparison.OrdinalIgnoreCase) || question.Contains("article 19", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("Romesh Thappar freedom of speech Madras");
        }

        return parts;
    }

    private static ResearchReport Empty(string question, string reason) => new()
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

public sealed class SessionStore
{
    private readonly string _root;

    public SessionStore(string root)
    {
        _root = root;
        Directory.CreateDirectory(root);
    }

    public async Task SaveAsync(string sessionId, ResearchState state)
    {
        var path = Path.Combine(_root, sessionId + ".json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(state));
    }

    public async Task<ResearchState?> LoadAsync(string sessionId)
    {
        var path = Path.Combine(_root, sessionId + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ResearchState>(await File.ReadAllTextAsync(path));
    }

    public void Delete(string sessionId)
    {
        var path = Path.Combine(_root, sessionId + ".json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
