# 08. Multi-agent research workflow

Series: Building AI Systems with .NET: From RAG to Agentic Workflows

Lab: [`labs/08-multi-agent`](../../labs/08-multi-agent)

## Engineering problem

Discovery, reading, citation checks, and the final report are different jobs. They still need one typed state.

## Learning objectives

- Move through discover, analyze, verify, and report.
- Analyze several cases with parallel tasks.
- Resume from a saved stage.

## Architecture

```text
ResearchState -> discovery -> Task.WhenAll analyses -> passage verification -> ResearchReport
```

## Implementation

One class, `ResearchWorkflow`, performs the four stages. Analysis uses `Task.WhenAll` over up to three cases. Verification calls `GetPassage` and `FindCitedCases`. The report fields are question, cases, findings, evidence, competing interpretations, limitations, and references.

## Commands

```bash
cd indian-legal-ai-lab
dotnet run --project labs/08-multi-agent
```

## Observed output

The speech question ends in stage `completed` and prints a verification line for each retrieved chunk.

## Tests and failures

`Session_can_resume_and_be_deleted` saves a state at `analyze`, resumes it, and expects `completed`.

## Tradeoffs

The four roles are stages, not separate model processes. That keeps the handoff typed. It does not show four independent model conversations.

## Limitations

Parallelism here is local CPU work. There is no network fan-out.

## Next

[Lab 09](../09/article.md)
