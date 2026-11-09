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

public sealed class ResearchAgent
{
    private readonly LegalResearchLibrary _library;
    private readonly int _maxCalls;
    private readonly TimeSpan _timeout;

    public ResearchAgent(LegalResearchLibrary library, int maxCalls = 4, TimeSpan? timeout = null)
    {
        _library = library;
        _maxCalls = maxCalls;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<AgentRunResult> RunAsync(string question, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        var logs = new List<AgentLog>();
        try
        {
            if (AdviceGuard.IsDisallowed(question))
            {
                return new AgentRunResult(false, "refused-advice-or-prediction", null, logs, 0);
            }

            if (string.IsNullOrWhiteSpace(question))
            {
                return new AgentRunResult(false, "empty-question", null, logs, 0);
            }

            timeout.Token.ThrowIfCancellationRequested();
            var search = _library.SearchCases(question, "hybrid", 3);
            logs.Add(new AgentLog("search_cases", search.Ok, search.Error ?? $"{search.Value!.Count} passages"));
            if (!search.Ok || search.Value!.Count == 0)
            {
                return new AgentRunResult(false, "insufficient-evidence", null, logs, logs.Count);
            }

            if (logs.Count >= _maxCalls)
            {
                return new AgentRunResult(false, "tool-call-limit", null, logs, logs.Count);
            }

            var top = search.Value[0];
            var judgment = _library.GetJudgment(top.CaseId, 800);
            logs.Add(new AgentLog("get_judgment", judgment.Ok, judgment.Error ?? top.CaseId));
            var statute = _library.LookupStatute(question.Contains("21", StringComparison.Ordinal) ? "article 21" : "article 19");
            logs.Add(new AgentLog("lookup_statute", statute.Ok, statute.Error ?? statute.Value!.Id));

            var client = new GroundedChatClient();
            var passages = string.Join("\n", search.Value.Select(p => $"PASSAGE: {p.Citation} [{p.ChunkId}] {p.Text}"));
            var response = await client.GetResponseAsync(
                [new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, question + "\n" + passages)],
                cancellationToken: timeout.Token);

            var brief = new CaseBrief
            {
                CaseId = top.CaseId,
                Citation = top.Citation,
                Title = top.Title,
                IsSynthetic = top.IsSynthetic,
                Summary = response.Text ?? "",
                SupportedByChunkIds = search.Value.Select(p => p.ChunkId).ToList(),
                UnverifiedStatements = CitationChecker.UnknownCitations(response.Text ?? "", _library.Records).ToList()
            };
            return new AgentRunResult(true, "completed", brief, logs, logs.Count);
        }
        catch (OperationCanceledException)
        {
            return new AgentRunResult(false, "timeout", null, logs, logs.Count);
        }
    }
}
