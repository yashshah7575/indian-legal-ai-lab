# 10. Memory and durable execution

Series: Building AI Systems with .NET: From RAG to Agentic Workflows

Lab: [`labs/10-memory`](../../labs/10-memory)

## Engineering problem

A research session should survive a restart, and the saved legal evidence should not be confused with chat history.

## Learning objectives

- Save and load `ResearchState`.
- Resume a workflow from the saved stage.
- Delete a session.

## Architecture

```text
ResearchState JSON -> SessionStore directory -> reload -> workflow continues
```

## Implementation

`SessionStore` writes one JSON file per session. The file is the workflow checkpoint: question, passages, stage, and report. It does not store a model transcript. `Delete` removes the file.

## Commands

```bash
cd indian-legal-ai-lab
dotnet run --project labs/10-memory
```

## Observed output

The lab saves stage `verify`, prints that stage, deletes the file, and prints `True` when the reload is null.

## Tests and failures

The same behavior is asserted in `Session_can_resume_and_be_deleted`.

## Tradeoffs

JSON files are obvious and easy to delete. They are not a database, and they have no retention clock beyond explicit deletion.

## Limitations

There is no encryption at rest. Do not point the store at a directory of sensitive client matters. This sample uses public judgments and fictional fixtures.

## Next

[Lab 11](../11/article.md)
