# 11. Evaluation, security, and reliability

Series: Building AI Systems with .NET: From RAG to Agentic Workflows

Lab: [`labs/11-evaluation`](../../labs/11-evaluation)

## Engineering problem

A legal assistant can fail by retrieving the wrong case, citing a fictional report, following instructions hidden in a judgment, or calling a tool that should not exist.

## Learning objectives

- Measure one fixed retrieval query.
- Detect an unknown fictional citation.
- Reject a tool that is not on the list.
- Keep injected instructions inside a fictional fixture from becoming a tool.

## Architecture

```text
fixed query -> hit@1\nanswer text -> CitationChecker\nMCP call -> allow list
```

## Implementation

The evaluation set is the committed corpus, not a downloaded IndicLegalQA split. IndicLegalQA was not added; its license and review were not completed in this run. The injection fixture `SYN-INJECT-001` says to call `delete_corpus`. The MCP server refuses that name. Live-model evaluations were not run.

## Commands

```bash
cd indian-legal-ai-lab
dotnet run --project labs/11-evaluation
```

## Observed output

The lab printed `recall_at_1=1` for the fixed query `Romesh Thappar freedom of speech Madras` under hybrid search, and `unknown_citations=1` for `FICTIONAL 2099 SYN 5`.

Lab 03 showed the contrasting failure: vector-only search for the Kharak Singh query ranked `SYN-INJECT-001` first.

## Tests and failures

Tool allow-list, citation checker, advice refusal, dead-letter, and API search tests. 17 tests passed in the local `dotnet test` run recorded in `docs/evaluations/results.md`.

## Tradeoffs

Deterministic checks catch regressions. They do not estimate how a hosted model would behave on a larger corpus.

## Limitations

No latency or token-cost numbers are reported because no hosted model was called. No claim is made about IndicLegalQA accuracy.

## Next

[Lab 12](../12/article.md)
