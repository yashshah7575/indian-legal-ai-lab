using Microsoft.Extensions.AI;

namespace LegalAiLab.Core;

/// <summary>
/// Offline stand-in for a chat model. It only restates supplied passages and never invents a citation.
/// </summary>
public sealed class GroundedChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var joined = string.Join("\n", messages.Select(m => m.Text ?? ""));
        if (AdviceGuard.IsDisallowed(joined))
        {
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ProductBoundary.Notice + " This question asks for advice or a prediction and will not be answered.")));
        }

        var passages = ExtractPassages(joined);
        if (passages.Count == 0)
        {
            const string ungrounded = "Ungrounded mode has no source passages. A citation such as FICTIONAL 2099 SYN 5 would be unsupported. No holding is stated.";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ungrounded)));
        }

        var lines = new List<string>
        {
            ProductBoundary.Notice,
            "Summary limited to the supplied passages:"
        };
        foreach (var passage in passages.Take(4))
        {
            lines.Add($"- {passage}");
        }

        lines.Add("Interpretations of these passages require human review.");
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Join("\n", lines))));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    private static List<string> ExtractPassages(string text)
    {
        var list = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("PASSAGE:", StringComparison.Ordinal))
            {
                list.Add(trimmed["PASSAGE:".Length..].Trim());
            }
        }

        return list;
    }
}

public static class CitationChecker
{
    public static IReadOnlyList<string> UnknownCitations(string text, IEnumerable<JudgmentRecord> catalog)
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in catalog)
        {
            known.Add(record.Citation);
            known.Add(record.CaseId);
            known.Add(record.NeutralCitation);
        }

        var found = new List<string>();
        var pattern = System.Text.RegularExpressions.Regex.Matches(text, @"FICTIONAL\s+\d{4}\s+SYN\s+\d+");
        foreach (System.Text.RegularExpressions.Match match in pattern)
        {
            if (!known.Contains(match.Value))
            {
                found.Add(match.Value);
            }
        }

        return found;
    }
}
