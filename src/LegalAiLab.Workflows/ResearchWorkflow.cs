using System.Text.Json;
using LegalAiLab.Core;

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
    private readonly CaseDiscovery _discovery;
    private readonly JudgmentAnalysis _analysis = new();
    private readonly CitationVerification _verification;
    private readonly ResearchReportWriter _reports = new();

    public ResearchWorkflow(LegalAiLab.Mcp.LegalResearchLibrary library)
    {
        _discovery = new CaseDiscovery(library);
        _verification = new CitationVerification(library);
    }

    public async Task<ResearchState> RunAsync(string question, ResearchState? resume = null)
    {
        var state = resume ?? new ResearchState { Question = question };
        if (AdviceGuard.IsDisallowed(question))
        {
            return Refuse(state, question);
        }

        DiscoverIfNeeded(state, question);
        if (state.Stage == "insufficient")
        {
            state.Report = _reports.Empty(question, "The local corpus did not contain enough matching passages.");
            return state;
        }

        await AnalyzeIfNeeded(state);
        VerifyIfNeeded(state);
        WriteIfNeeded(state, question);
        return state;
    }

    public static IReadOnlyList<string> Decompose(string question) => new QueryDecomposer().Decompose(question);

    private ResearchState Refuse(ResearchState state, string question)
    {
        state.Stage = "refused";
        state.Report = _reports.Empty(question, "The question asks for advice or an outcome prediction.");
        return state;
    }

    private void DiscoverIfNeeded(ResearchState state, string question)
    {
        if (state.Stage is not ("new" or "discover"))
        {
            return;
        }

        state.Discovered = _discovery.Discover(question);
        state.Stage = state.Discovered.Count == 0 ? "insufficient" : "analyze";
    }

    private async Task AnalyzeIfNeeded(ResearchState state)
    {
        if (state.Stage != "analyze")
        {
            return;
        }

        state.Analyses = await _analysis.AnalyzeAsync(state.Discovered);
        state.Stage = "verify";
    }

    private void VerifyIfNeeded(ResearchState state)
    {
        if (state.Stage != "verify")
        {
            return;
        }

        state.VerificationNotes = _verification.Verify(state.Discovered);
        state.Stage = "report";
    }

    private void WriteIfNeeded(ResearchState state, string question)
    {
        if (state.Stage != "report")
        {
            return;
        }

        state.Report = _reports.Write(question, state);
        state.Stage = "completed";
    }
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
        await File.WriteAllTextAsync(PathFor(sessionId), JsonSerializer.Serialize(state));
    }

    public async Task<ResearchState?> LoadAsync(string sessionId)
    {
        var path = PathFor(sessionId);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ResearchState>(await File.ReadAllTextAsync(path));
    }

    public void Delete(string sessionId)
    {
        var path = PathFor(sessionId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string PathFor(string sessionId) => Path.Combine(_root, sessionId + ".json");
}
