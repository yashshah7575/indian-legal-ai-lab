# Code audit

Recorded while the solution targeted `net8.0`. Corrections below were applied in the same change unless a blocker is named.

| Issue | Where | Severity | Why it matters | Correction | Acceptance |
| --- | --- | --- | --- | --- | --- |
| Hash vectors described as the only embedding path | `HashEmbeddingGenerator` | High | They are not semantic embeddings | `EmbeddingService` calls `/v1/embeddings` when a base URL and HTTP client exist and labels that result semantic. Otherwise it returns the hash vector as `hash-offline`. | `Embedding_without_a_provider_is_labeled_offline` passes. No live embedding endpoint was called. |
| Custom JSON-RPC presented as MCP | `McpStdioServer` | High | It is a teaching handler, not the official SDK | Official host is `OfficialMcpHost` using `ModelContextProtocol` 1.4.0 (`net8.0`). Lab 05 prints that host first. The teaching handler remains. | `Mcp_stdio_client_lists_and_calls_search` starts `LegalAiLab.McpHost` and calls `lookup_statute`. |
| Research starts from the page query string | `Research.razor` | Medium | A GET rerun repeats the workflow | The form is `method="post"` and binds `SupplyParameterFromForm`. | Opening `/research` does not submit a question. |
| Session writes are not atomic and ids are unchecked | `SessionStore` | Medium | A crash or `../` id can corrupt or escape the directory | Temp file plus `File.Move`. Ids must be letters and digits. | `Session_id_rejects_path_traversal` passes. |
| Ingestion queue dies with the process | `IngestionPipeline` | Medium | Restart loses jobs | `FileIngestionQueue` stores pending, done, and dead JSON. The in-memory pipeline remains the worker. | `Durable_queue_skips_a_completed_job_id` passes. |
| Keyword overlap is the only lexical ranker | `KeywordScorer` | Medium | Shared legal words dominate | `bm25` is a separate method. | `Bm25_finds_kharak_singh` returned `1962 INSC 389`. |
| `Program.Main` still composes the host | API and Web `Program` | Low | Acceptable for the composition root | Left as the composition root. | Hosts build. |
| pgvector was SQL text only | `PgVectorSql` | Medium | Not a verified database | Unchanged. Docker is still not installed here. | Do not treat the SQL test as a database run. |
| Agent loop is not model-directed | `ResearchAgent` | High | A shared budget and a fixed sequence hide tool choice | Each `RunAsync` creates its own `ToolCallBudget`. An injected `IChatClient` that returns `FunctionCallContent` for `search_cases` selects that tool. No tool call falls back to the fixed sequence. | `Agent_budget_resets_between_runs` and `Agent_executes_a_model_selected_tool` pass. The default client is still the offline stub. |
| One query was treated as retrieval quality | Lab 11 | Medium | It is a fixture, not a benchmark | BM25 adds a second measured query. No Recall@K table beyond the existing single hybrid query was invented. | Recorded results only. |

`dotnet test`: 27 passed, 0 failed, after the later correctness pass. No live model was called. The earlier 17-pass and 21-pass notes are historical.
