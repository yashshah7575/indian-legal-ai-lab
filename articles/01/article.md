# 01. LLM fundamentals in C#

Series: Building AI Systems with .NET: From RAG to Agentic Workflows

Lab: [`labs/01-llm-fundamentals`](../../labs/01-llm-fundamentals)

## Engineering problem

A researcher pastes a judgment and asks for a brief. A chat model can sound precise while naming a case that was never supplied.

## Learning objectives

- See a prompt, a context window, and a structured response.
- Separate a grounded summary from an ungrounded one.
- Check citations against the catalog.

## Architecture

```text
judgment text -> PASSAGE lines -> GroundedChatClient -> summary
bare prompt -> same client -> explicit lack of sources
```

## Implementation

`GroundedChatClient` implements `IChatClient` from Microsoft.Extensions.AI. If the prompt contains lines that start with `PASSAGE:`, those lines are the only material restated. If none are present, the client says there are no source passages and mentions `FICTIONAL 2099 SYN 5` as an example of an unsupported citation. `CitationChecker` then flags that identifier because it is not in the manifest.

## Commands

```bash
cd indian-legal-ai-lab
dotnet run --project labs/01-llm-fundamentals
```

## Observed output

The grounded path prints the educational notice and a passage from Romesh Thappar, `[1950] 1 S.C.R. 594`. The bare path states that ungrounded mode has no source passages.

## Tests and failures

`Ungrounded_client_does_not_invent_a_known_holding` expects the unsupported fictional citation to be detected.

## Tradeoffs

A deterministic client makes the lab reproducible. It is not a language model and does not measure fluency. A hosted model needs the same passage contract or it will cite from memory.

## Limitations

Token counts are not measured here. The client does not call a remote model. This is not legal advice.

## Next

[Lab 02](../02/article.md)
