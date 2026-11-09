# 07. Agentic RAG and precedent research

Series: Building AI Systems with .NET: From RAG to Agentic Workflows

Lab: [`labs/07-agentic-rag`](../../labs/07-agentic-rag)

## Engineering problem

A privacy question may need more than one query, and a citation string is not a ruling that one case overrules another.

## Learning objectives

- Decompose a question.
- Stop when evidence is missing.
- Keep a human-review flag.
- Avoid an automatic overruling claim.

## Architecture

```text
question -> extra queries (privacy, speech) -> merged passages -> sufficiency check -> report
```

## Implementation

`ResearchWorkflow.Decompose` adds a Kharak Singh query when the question mentions privacy, and a Thappar query when it mentions speech or Article 19. If nothing is retrieved, the stage is `insufficient`. Citation links built from the text say they are string matches and do not mean a later case overrules an earlier one.

## Commands

```bash
cd indian-legal-ai-lab
dotnet run --project labs/07-agentic-rag
```

## Observed output

A privacy question completes with `1962 INSC 389` among the considered cases when Kharak Singh's extracted text is in the index. `HumanReview` is `Required before any reliance.`

## Tests and failures

`Workflow_reports_limitations_for_privacy_question` expects that case id and a limitation about string matches. Puttaswamy is metadata only, so the workflow cannot quote its text.

## Tradeoffs

Hand-written query expansion is transparent. It misses phrasings that do not contain the keywords.

## Limitations

The famous privacy line is not fully in the text index. Maneka Gandhi and Puttaswamy are listed as metadata because their PDFs were large and were not vendored.

## Next

[Lab 08](../08/article.md)
