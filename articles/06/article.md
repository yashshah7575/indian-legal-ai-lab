# 06. A bounded research agent

Series: Building AI Systems with .NET: From RAG to Agentic Workflows

Lab: [`labs/06-research-agent`](../../labs/06-research-agent)

## Engineering problem

An agent that can call tools can also loop, time out, or answer a question it should refuse.

## Learning objectives

- Cap tool calls.
- Apply a timeout.
- Refuse outcome predictions.
- Attach chunk ids to the brief.

## Architecture

```text
question -> refuse? -> search_cases -> get_judgment -> lookup_statute -> GroundedChatClient -> CaseBrief
```

## Implementation

`ResearchAgent` allows four tool calls and five seconds by default. `AdviceGuard` stops questions such as "Will I win my case?". The brief summary comes only from `PASSAGE:` lines built from search hits. Microsoft Agent Framework is not referenced; this machine has the .NET 8 SDK, and the lab uses a short C# loop plus Microsoft.Extensions.AI.

## Commands

```bash
cd indian-legal-ai-lab
dotnet run --project labs/06-research-agent
```

## Observed output

For a Thappar speech question the outcome is `completed`, the summary cites the supplied passage, and the log lists `search_cases`, `get_judgment`, and `lookup_statute`.

## Tests and failures

`Agent_refuses_outcome_prediction` and `Agent_brief_is_grounded_in_passages`.

## Tradeoffs

The loop is predictable. It does not plan with a hosted model, so it cannot demonstrate model-chosen tool order.

## Limitations

Timeouts are real cancellation tokens, but the local tools return immediately, so the timeout path is not exercised by the default lab run.

## Next

[Lab 07](../07/article.md)
