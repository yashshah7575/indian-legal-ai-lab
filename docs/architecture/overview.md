# Architecture

```text
sample-data/manifest.json
        |
        v
LegalAiLab.Core  records, statute snippets, GroundedChatClient, citation check
        |
        v
LegalAiLab.Ingestion  chunk, dedupe, queue, public-bucket metadata download
        |
        v
LegalAiLab.Retrieval  hash vectors, keyword, hybrid, pgvector SQL text
        |
        v
LegalAiLab.Mcp  tools and JSON-RPC
        |
        +--> LegalAiLab.Agents  bounded loop
        +--> LegalAiLab.Workflows  stages, sessions, report
                    |
                    +--> LegalAiLab.Api
                    +--> LegalAiLab.Web
```

The product answers research questions from retrieved passages. It refuses outcome-prediction prompts in `AdviceGuard`.

Dataset: [Indian Supreme Court Judgments](https://registry.opendata.aws/indian-supreme-court-judgments/), CC-BY-4.0, bucket `indian-supreme-court-judgments`. Two English PDFs were downloaded and extracted with pypdf while preparing the sample:

- `1950_1_594_605_EN.pdf` — Romesh Thappar, neutral citation `1950INSC14`
- `1964_1_332_361_EN.pdf` — Kharak Singh, case id `1962 INSC 389`

Other manifest rows for A.K. Gopalan, Maneka Gandhi, Puttaswamy (2017 and 2018), Navtej Singh Johar, and Shayara Bano are metadata only. Full text was not copied because the PDFs are large. Fictional rows are marked `isSynthetic: true` and titled `FICTIONAL`.

Target framework is `net8.0` because the installed SDKs stop at 8.0.403. Microsoft.Extensions.AI 9.5.0 is referenced. Microsoft Agent Framework and the official MCP C# SDK are not referenced.
