# Code audit

Recorded while the solution targeted `net8.0`. Corrections below were applied in the same change unless a blocker is named.

| Issue | Where | Severity | Why it matters | Correction | Acceptance |
| --- | --- | --- | --- | --- | --- |
| Hash vectors described as the only embedding path | `HashEmbeddingGenerator` | High | They are not semantic embeddings | Kept as the offline baseline. Live chat uses `OpenAiCompatibleChatClient` when `LEGALAI_CHAT_BASE_URL` is set. An embedding HTTP client was not added in this pass because no embedding endpoint was available to verify. | Offline tests still pass. Live chat is covered by a stub HTTP handler, not a live model. |
| Custom JSON-RPC presented as MCP | `McpStdioServer` | High | It is a teaching handler, not the official SDK | Official host is `OfficialMcpHost` using `ModelContextProtocol` 1.4.0 (`net8.0`). The teaching handler remains for the lab that prints JSON. | `LegalAiLab.Mcp` builds with the SDK. A stdio client round-trip was not executed. |
| Research starts from the page query string | `Research.razor` | Medium | A GET rerun repeats the workflow | The form is `method="post"` and binds `SupplyParameterFromForm`. | Opening `/research` does not submit a question. |
| Session writes are not atomic and ids are unchecked | `SessionStore` | Medium | A crash or `../` id can corrupt or escape the directory | Temp file plus `File.Move`. Ids must be letters and digits. | `Session_id_rejects_path_traversal` passes. |
| Ingestion queue dies with the process | `IngestionPipeline` | Medium | Restart loses jobs | `FileIngestionQueue` stores pending, done, and dead JSON. The in-memory pipeline remains the worker. | `Durable_queue_skips_a_completed_job_id` passes. |
| Keyword overlap is the only lexical ranker | `KeywordScorer` | Medium | Shared legal words dominate | `bm25` is a separate method. | `Bm25_finds_kharak_singh` returned `1962 INSC 389`. |
| `Program.Main` still composes the host | API and Web `Program` | Low | Acceptable for the composition root | Left as the composition root. | Hosts build. |
| pgvector was SQL text only | `PgVectorSql` | Medium | Not a verified database | Unchanged. Docker is still not installed here. | Do not treat the SQL test as a database run. |
| Agent loop is not model-directed | `ResearchAgent` | High | It always calls the same three tools | Not replaced with a live tool-calling loop in this pass. The live chat client can be called, but the agent still uses `GroundedChatClient`. | Articles must keep calling this a fixed loop. |
| One query was treated as retrieval quality | Lab 11 | Medium | It is a fixture, not a benchmark | BM25 adds a second measured query. No Recall@K table beyond the existing single hybrid query was invented. | Recorded results only. |

`dotnet test`: 21 passed, 0 failed, after these changes. No live model was called.
