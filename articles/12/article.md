# 12. Production architecture and the application

Series: Building AI Systems with .NET: From RAG to Agentic Workflows

Lab: [`labs/12-production`](../../labs/12-production)

## Engineering problem

The labs have to meet in one place a person can run: search, case text, a research report, ingestion status, and a health check.

## Learning objectives

- Run an ASP.NET Core API and a Blazor UI.
- Trace requests with OpenTelemetry.
- Export a report from the saved session.
- Keep AWS optional.

## Architecture

```text
Blazor SSR -> LegalResearchLibrary / ResearchWorkflow -> sample-data
API /health /cases /research /ingest -> same libraries
```

## Implementation

`LegalAiLab.Api` maps health, case search, research, session delete, ingest, and citation routes. `LegalAiLab.Web` is a Blazor Server-capable app using static render and antiforgery for the forms. Pages cover home, search, case detail, research, ingestion status, and history. OpenTelemetry registers ASP.NET Core instrumentation and an activity source named `LegalAiLab`. No exporter is configured, so traces stay in-process unless you add one.

## Commands

```bash
cd indian-legal-ai-lab
dotnet run --project src/LegalAiLab.Api --urls http://127.0.0.1:5088
dotnet run --project src/LegalAiLab.Web --urls http://127.0.0.1:5089
```

## Observed output

During this build, `GET /health` returned `Healthy`. `GET /cases?q=Romesh%20Thappar%20speech` returned case `1950 INSC 14`. `GET /search?q=Romesh%20Thappar` on the Blazor app returned HTTP 200 and the page contained that case. Docker Compose was not run because Docker was not available.

## Tests and failures

`Health_and_search_work` and `Research_refuses_prediction` use `WebApplicationFactory`.

## Tradeoffs

One process and a file corpus are enough for a lab. They are not a multi-user deployment. pgvector and S3 remain optional.

## Limitations

The UI was checked with curl, not a browser session. .NET 10 is not installed here; the solution targets `net8.0`.

## Next

The series stops here. The research page on a personal site, if you keep one, should stay separate from this tutorial list.
