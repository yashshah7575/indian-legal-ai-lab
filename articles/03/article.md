# 03. Advanced legal retrieval

Series: Building AI Systems with .NET: From RAG to Agentic Workflows

Lab: [`labs/03-advanced-retrieval`](../../labs/03-advanced-retrieval)

## Engineering problem

A single similarity score is a weak way to search case law. Court, year, and citation are part of the query.

## Learning objectives

- Filter by court and year.
- Compare keyword, vector, and hybrid ranking on one question.
- See the SQL a pgvector store would run.

## Architecture

```text
query + filters -> keyword score\n                  -> hash cosine\n                  -> hybrid 0.55/0.45 -> passages
```

## Implementation

`LegalIndex.Search` accepts `keyword`, `vector`, or `hybrid`. Filters drop rows before scoring. `PgVectorSql` holds the `vector(64)` table and a filtered nearest-neighbour statement. The SQL is not executed in the unit tests.

## Commands

```bash
cd indian-legal-ai-lab
dotnet run --project labs/03-advanced-retrieval
```

## Observed output

On this machine the query `Kharak Singh domiciliary visits` returned:

- keyword: `1962 INSC 389` score 4.000
- vector: `SYN-INJECT-001` score 0.316
- hybrid: `1962 INSC 389` score 0.631

The vector-only rank preferred a fictional fixture. That is a real result of the hashing embedder, not a live reranker.

## Tests and failures

`Year_filter_excludes_other_years` checks the 2024 filter. `Pgvector_sql_mentions_filters` checks the SQL text.

## Tradeoffs

Hybrid search corrected this particular query. It will not correct every query. A hosted embedding model and a reranker are still optional and were not run.

## Limitations

PostgreSQL was not started. Docker is not installed in the environment used for this build.

## Next

[Lab 04](../04/article.md)
