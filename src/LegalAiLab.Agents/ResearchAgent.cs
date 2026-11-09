using LegalAiLab.Core;
using LegalAiLab.Mcp;

namespace LegalAiLab.Agents;

public sealed record AgentLog(string Tool, bool Ok, string Detail);

public sealed record AgentRunResult(
    bool Ok,
    string Outcome,
    CaseBrief? Brief,
    IReadOnlyList<AgentLog> Logs,
    int ToolCalls);

public sealed class ToolCallBudget
{
    private readonly int _maxCalls;
    private int _used;

    public ToolCallBudget(int maxCalls) => _maxCalls = maxCalls;

    public bool CanCall => _used < _maxCalls;

    public void Record() => _used++;

    public int Used => _used;
}

public sealed class PassagePromptBuilder
{
    public string Build(string question, IReadOnlyList<RetrievedPassage> passages)
    {
        var lines = passages.Select(passage => $"PASSAGE: {passage.Citation} [{passage.ChunkId}] {passage.Text}");
        return question + "\n" + string.Join("\n", lines);
    }
}

public sealed class CaseBriefFactory
{
    public CaseBrief Create(RetrievedPassage top, string summary, IReadOnlyList<RetrievedPassage> passages, IReadOnlyList<JudgmentRecord> catalog)
    {
        return new CaseBrief
        {
            CaseId = top.CaseId,
            Citation = top.Citation,
            Title = top.Title,
            IsSynthetic = top.IsSynthetic,
            Summary = summary,
            SupportedByChunkIds = passages.Select(passage => passage.ChunkId).ToList(),
            UnverifiedStatements = CitationChecker.UnknownCitations(summary, catalog).ToList()
        };
    }
}

public sealed class ResearchAgent
{
    private readonly LegalResearchLibrary _library;
    private readonly ToolCallBudget _budget;
    private readonly TimeSpan _timeout;
    private readonly PassagePromptBuilder _prompts = new();
    private readonly CaseBriefFactory _briefs = new();

    public ResearchAgent(LegalResearchLibrary library, int maxCalls = 4, TimeSpan? timeout = null)
    {
        _library = library;
        _budget = new ToolCallBudget(maxCalls);
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<AgentRunResult> RunAsync(string question, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        var logs = new List<AgentLog>();
        try
        {
            var refused = RefuseIfNeeded(question, logs);
            if (refused is not null)
            {
                return refused;
            }

            timeout.Token.ThrowIfCancellationRequested();
            var search = Search(question, logs);
            if (!search.Ok || search.Value!.Count == 0 || !_budget.CanCall)
            {
                return Stop(search, logs);
            }

            var top = search.Value[0];
            ReadJudgment(top, logs);
            LookupStatute(question, logs);
            var brief = await SummarizeAsync(question, search.Value, top, timeout.Token);
            return new AgentRunResult(true, "completed", brief, logs, logs.Count);
        }
        catch (OperationCanceledException)
        {
            return new AgentRunResult(false, "timeout", null, logs, logs.Count);
        }
    }

    private AgentRunResult? RefuseIfNeeded(string question, List<AgentLog> logs)
    {
        if (AdviceGuard.IsDisallowed(question))
        {
            return new AgentRunResult(false, "refused-advice-or-prediction", null, logs, 0);
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            return new AgentRunResult(false, "empty-question", null, logs, 0);
        }

        return null;
    }

    private ToolResult<IReadOnlyList<RetrievedPassage>> Search(string question, List<AgentLog> logs)
    {
        var search = _library.SearchCases(question, "hybrid", 3);
        _budget.Record();
        logs.Add(new AgentLog("search_cases", search.Ok, search.Error ?? $"{search.Value!.Count} passages"));
        return search;
    }

    private AgentRunResult Stop(ToolResult<IReadOnlyList<RetrievedPassage>> search, List<AgentLog> logs)
    {
        var outcome = !_budget.CanCall && search.Ok ? "tool-call-limit" : "insufficient-evidence";
        return new AgentRunResult(false, outcome, null, logs, logs.Count);
    }

    private void ReadJudgment(RetrievedPassage top, List<AgentLog> logs)
    {
        var judgment = _library.GetJudgment(top.CaseId, 800);
        _budget.Record();
        logs.Add(new AgentLog("get_judgment", judgment.Ok, judgment.Error ?? top.CaseId));
    }

    private void LookupStatute(string question, List<AgentLog> logs)
    {
        var query = question.Contains("21", StringComparison.Ordinal) ? "article 21" : "article 19";
        var statute = _library.LookupStatute(query);
        _budget.Record();
        logs.Add(new AgentLog("lookup_statute", statute.Ok, statute.Error ?? statute.Value!.Id));
    }

    private async Task<CaseBrief> SummarizeAsync(string question, IReadOnlyList<RetrievedPassage> passages, RetrievedPassage top, CancellationToken cancellationToken)
    {
        var client = new GroundedChatClient();
        var response = await client.GetResponseAsync(
            [new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, _prompts.Build(question, passages))],
            cancellationToken: cancellationToken);
        return _briefs.Create(top, response.Text ?? "", passages, _library.Records);
    }
}
