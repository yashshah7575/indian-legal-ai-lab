# Indian Legal AI Lab

Educational research over a small public subset of Indian Supreme Court judgments. It is not legal advice and it does not predict outcomes.

Repository: https://github.com/yashshah7575/indian-legal-ai-lab

## Architecture

`LegalAiLab.Core` holds records, the offline `GroundedChatClient`, and `OpenAiCompatibleChatClient`. `Ingestion` chunks text and can queue jobs on disk. `Retrieval` ranks with keyword, BM25, hashing vectors, or a hybrid of keyword and hashing vectors. Hashing vectors are an offline baseline, not a semantic embedding model. `Mcp` has a teaching JSON handler and `OfficialMcpHost` on the official MCP C# SDK 1.4.0. `Agents` runs a fixed tool loop. `Workflows` runs staged research and stores sessions as JSON.

## Run

```bash
dotnet test
dotnet run --project src/LegalAiLab.Api --urls http://127.0.0.1:5088
dotnet run --project src/LegalAiLab.Web --urls http://127.0.0.1:5089
```

The Blazor research form runs only after you submit it.

## Live model

Set `LEGALAI_CHAT_BASE_URL` to an OpenAI-compatible base such as `http://localhost:11434/` for Ollama, and optionally `LEGALAI_CHAT_MODEL` and `LEGALAI_API_KEY`. The default labs do not call that client. No live model was executed in the recorded test run.

Official MCP stdio host:

```bash
dotnet run --project src/LegalAiLab.McpHost
```

That host project is added only if present. See `OfficialMcpHost` in `LegalAiLab.Mcp`.

## Tests

`dotnet test` is offline. The last recorded run was 21 passed, 0 failed.

## Tutorials

The articles are published on https://yashshah.ai/publications. Each lab README links to its article.

## Limits

Docker was not available, so pgvector was not started. The agent loop does not ask a model to choose tools. Semantic embeddings are not included.

## Data

[Indian Supreme Court Judgments](https://registry.opendata.aws/indian-supreme-court-judgments/), CC-BY-4.0. Two extracted judgments are vendored. Other rows are metadata. Fictional fixtures are titled `FICTIONAL`. See `LICENSE`.
