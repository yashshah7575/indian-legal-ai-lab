# 09. Event-driven ingestion

Series: Building AI Systems with .NET: From RAG to Agentic Workflows

Lab: [`labs/09-event-ingestion`](../../labs/09-event-ingestion)

## Engineering problem

A new judgment should be extracted, checked for duplicates, chunked, and indexed once, even if the message is delivered twice.

## Learning objectives

- Queue a job.
- Retry a failed extraction.
- Dead-letter a job that keeps failing.
- Skip a job id that already completed.

## Architecture

```text
enqueue -> attempt extraction -> chunks or incomplete or dead-letter
```

## Implementation

`IngestionPipeline` is an in-memory queue. `FailTimes` throws `IOException` for the first N attempts. After `maxAttempts`, the job is dead-lettered. A second enqueue of the same job id returns `idempotent-skip`. Missing text returns `incomplete-document`. `DuplicateDetector` hashes neutral citation, citation, and date.

## Commands

```bash
cd indian-legal-ai-lab
dotnet run --project labs/09-event-ingestion
```

## Observed output

The Thappar job is configured to fail once and then complete. The lab prints the outcome and a chunk count greater than zero.

## Tests and failures

`Pipeline_retries_then_dead_letters` and `Pipeline_is_idempotent_and_marks_incomplete_text`.

## Tradeoffs

A local queue needs no AWS account. It dies with the process. The AWS note in `infrastructure/aws` describes SQS only as a later option.

## Limitations

Embeddings are not stored in Postgres in this lab. The pipeline stores chunks in memory.

## Next

[Lab 10](../10/article.md)
