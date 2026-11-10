# Evaluation results

Recorded on this machine after `dotnet test tests/LegalAiLab.Tests/LegalAiLab.Tests.csproj`.

- Result: Passed. Failed 0, Passed 27, Skipped 0. Earlier notes of 17 and 21 are historical.
- Runtime: .NET 8 (`8.0.403` is the SDK selected by `global.json`; newer 8.0 feature-band SDKs are allowed).
- Hosted LLM: not called.
- IndicLegalQA: not downloaded.
- Docker: `docker version` was not available, so Compose and the pgvector container were not started.

Fixed hybrid query `Romesh Thappar freedom of speech Madras`: top hit `1950 INSC 14` (`recall_at_1=1` in lab 11).

Fixed query `Kharak Singh domiciliary visits`:

| Method | Top case | Score |
| --- | --- | --- |
| keyword | 1962 INSC 389 | 4.000 |
| vector | SYN-INJECT-001 | 0.316 |
| hybrid | 1962 INSC 389 | 0.631 |

`SYN-INJECT-001` is a fictional fixture. Vector-only search ranked it above the Kharak Singh text for that query.

API smoke, after `dotnet run --project src/LegalAiLab.Api --urls http://127.0.0.1:5088`:

- `GET /health` body: `Healthy`
- `GET /cases?q=Romesh%20Thappar%20speech` top case id: `1950 INSC 14`

Blazor smoke, after antiforgery middleware was added, `GET http://127.0.0.1:5089/search?q=Romesh%20Thappar` returned HTTP 200 and the HTML contained the case citation.
