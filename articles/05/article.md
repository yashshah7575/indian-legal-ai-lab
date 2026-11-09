# 05. An MCP server in C#

Series: Building AI Systems with .NET: From RAG to Agentic Workflows

Lab: [`labs/05-mcp-server`](../../labs/05-mcp-server)

## Engineering problem

A research assistant in another process needs the same read-only tools without sharing the C# object graph.

## Learning objectives

- Serve `initialize`, `tools/list`, `resources/list`, `prompts/list`, and `tools/call`.
- Keep the tool list closed.
- Reuse the library from Lab 04.

## Architecture

```text
JSON-RPC request -> McpStdioServer -> LegalResearchLibrary -> JSON-RPC result
```

## Implementation

`McpStdioServer.Handle` parses one JSON-RPC object. The tools are `search_cases`, `get_case_metadata`, `get_judgment`, `find_cited_cases`, and `lookup_statute`. A call named `delete_corpus` returns an allow-list error inside the tool payload. One resource points at Article 21. One prompt is named `case-brief`. This is an in-process JSON-RPC handler used by tests and the lab, not a full stdio framing host from the official MCP C# SDK.

## Commands

```bash
cd indian-legal-ai-lab
dotnet run --project labs/05-mcp-server
```

## Observed output

The process prints an `initialize` result with server name `indian-legal-ai-lab` and a `tools/list` result containing the five tool names.

## Tests and failures

`Mcp_lists_read_only_tools` asserts those names. The delete-corpus call is rejected.

## Tradeoffs

A small handler is easy to test. It does not implement every MCP capability, progress notification, or the official SDK's transport.

## Limitations

The official MCP C# SDK package is not referenced. Prompts are advertised, not expanded into a model call.

## Next

[Lab 06](../06/article.md)
