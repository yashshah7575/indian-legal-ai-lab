# Indian Legal AI Lab

Repository: https://github.com/yashshah7575/indian-legal-ai-lab

An educational .NET application for researching a small public subset of Indian Supreme Court judgments. It shows the path from a grounded summary to retrieval, tools, a JSON-RPC tool server, a bounded agent, a staged workflow, ingestion, checkpoints, and a Blazor UI.

This is not a lawyer, not a prediction system, and not a filing tool.

## Run

```bash
dotnet test
dotnet run --project labs/01-llm-fundamentals
dotnet run --project src/LegalAiLab.Api --urls http://127.0.0.1:5088
dotnet run --project src/LegalAiLab.Web --urls http://127.0.0.1:5089
```

Open `http://127.0.0.1:5089/search?q=Romesh%20Thappar`.

The solution targets `net8.0` (`global.json` asks for SDK 8.0.403 or a later 8.0 feature band). .NET 10 was not installed on the build machine.

## Data

`sample-data/manifest.json` is checksummed. It includes extracted text for Romesh Thappar (`1950INSC14`) and Kharak Singh (`1962 INSC 389`), metadata for several other public judgments, and fictional fixtures whose titles start with `FICTIONAL`.

Source: [Indian Supreme Court Judgments on AWS](https://registry.opendata.aws/indian-supreme-court-judgments/), CC-BY-4.0. The national corpus is not loaded. Court websites were not scraped.

`PublicBucketBootstrap` can download one metadata JSON from the public HTTPS endpoint when you pass a year and a `.json` file name.

## Layout

- Write-ups for the twelve labs are published on https://yashshah.ai/publications
- `labs/01-llm-fundamentals` through `labs/12-production`
- `src/LegalAiLab.*` — core, ingestion, retrieval, MCP tools, agent, workflow, API, Blazor
- `docs/evaluations/results.md` — commands that were actually run

## Not claimed

Hosted-model quality, IndicLegalQA scores, a completed Docker run, Microsoft Agent Framework, the official MCP SDK, and any edit to yashshah.ai. Integration notes are in `docs/website/integration.md`.
