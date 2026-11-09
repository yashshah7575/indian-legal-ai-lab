# 04. Function calling and research tools

Series: Building AI Systems with .NET: From RAG to Agentic Workflows

Lab: [`labs/04-function-calling`](../../labs/04-function-calling)

## Engineering problem

The model should not be the system of record for a citation, a statute, or a page of the judgment.

## Learning objectives

- Call C# tools with validated arguments.
- Return typed failures.
- Read a short constitutional provision from a local catalog.

## Architecture

```text
caller -> LegalResearchLibrary -> ToolResult<T> -> metadata | passage | statute | error
```

## Implementation

`SearchCases` rejects a blank query and a `take` outside 1–20. `GetCaseMetadata` strips the full text. `GetPassage` loads a chunk by id. `LookupStatute` searches three short provisions: Article 19(1)(a), Article 19(2), and Article 21. Those strings are included as lookup text, not as a commentary.

## Commands

```bash
cd indian-legal-ai-lab
dotnet run --project labs/04-function-calling
```

## Observed output

The lab prints the Thappar title, the Article 21 sentence, and the error `Query text is required.`

## Tests and failures

`Tools_reject_blank_input_and_unknown_tools` covers the blank query and the Article 21 hit.

## Tradeoffs

Tools are easy to test and boring on purpose. They do not draft pleadings.

## Limitations

The statute catalog is three provisions. It is not the Constitution.

## Next

[Lab 05](../05/article.md)
