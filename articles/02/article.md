# 02. Simple RAG over court judgments

Series: Building AI Systems with .NET: From RAG to Agentic Workflows

Lab: [`labs/02-simple-rag`](../../labs/02-simple-rag)

## Engineering problem

The model context cannot hold every judgment, and a summary is only as trustworthy as the passages placed in front of it.

## Learning objectives

- Load the versioned manifest.
- Chunk extracted text with character offsets.
- Rank chunks with a local vector and show the source span.

## Architecture

```text
manifest.json -> JudgmentRecord -> TextChunker -> hash embedding -> cosine search -> passage
```

## Implementation

`CorpusLoader` checks `manifest.sha256` before reading records. `TextChunker` cuts about 900 characters with overlap. `HashEmbeddingGenerator` builds a 64-dimensional bag-of-tokens vector so the index runs offline. Each hit keeps `ChunkId`, start, and end.

## Commands

```bash
cd indian-legal-ai-lab
dotnet run --project labs/02-simple-rag
```

## Observed output

The lab prints the top three vector hits for a Romesh Thappar speech query, including case id `1950 INSC 14` and a chunk id.

## Tests and failures

`Hybrid_search_finds_thappar_for_speech_query` locks the top hybrid hit to that case.

## Tradeoffs

Hashing embeddings need no model download. They also confuse short overlapping legal vocabulary, which Lab 03 measures.

## Limitations

Only two judgments have extracted text from the public PDFs. The rest of the real records are metadata until a PDF is fetched and extracted.

## Next

[Lab 03](../03/article.md)
