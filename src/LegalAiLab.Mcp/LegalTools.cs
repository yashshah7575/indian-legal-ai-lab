using System.Text.Json;
using LegalAiLab.Core;
using LegalAiLab.Ingestion;
using LegalAiLab.Retrieval;

namespace LegalAiLab.Mcp;

public sealed class LegalResearchLibrary
{
    private readonly IReadOnlyList<JudgmentRecord> _records;
    private readonly LegalIndex _index;
    private readonly IReadOnlyList<CitationLink> _links;

    public LegalResearchLibrary(IReadOnlyList<JudgmentRecord>? records = null)
    {
        _records = records ?? CorpusLoader.Load();
        _index = LegalIndex.FromRecords(_records);
        _links = CitationGraphBuilder.Build(_records);
    }

    public IReadOnlyList<JudgmentRecord> Records => _records;
    public LegalIndex Index => _index;
    public IReadOnlyList<CitationLink> Links => _links;

    public ToolResult<IReadOnlyList<RetrievedPassage>> SearchCases(string query, string method = "hybrid", int take = 5, SearchFilters? filters = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResult<IReadOnlyList<RetrievedPassage>>.Fail("Query text is required.");
        }

        if (take is < 1 or > 20)
        {
            return ToolResult<IReadOnlyList<RetrievedPassage>>.Fail("take must be between 1 and 20.");
        }

        try
        {
            return ToolResult<IReadOnlyList<RetrievedPassage>>.Success(_index.Search(query, method, take, filters));
        }
        catch (ArgumentException ex)
        {
            return ToolResult<IReadOnlyList<RetrievedPassage>>.Fail(ex.Message);
        }
    }

    public ToolResult<JudgmentRecord> GetCaseMetadata(string caseId)
    {
        var record = Find(caseId);
        return record is null
            ? ToolResult<JudgmentRecord>.Fail("No case matched that identifier.")
            : ToolResult<JudgmentRecord>.Success(record with { Text = null });
    }

    public ToolResult<string> GetJudgment(string caseId, int maxChars = 1500)
    {
        if (maxChars is < 100 or > 8000)
        {
            return ToolResult<string>.Fail("maxChars must be between 100 and 8000.");
        }

        var record = Find(caseId);
        if (record?.Text is null)
        {
            return ToolResult<string>.Fail(record is null ? "Unknown case." : "No extracted text is stored for this judgment.");
        }

        var text = record.Text.Length <= maxChars ? record.Text : record.Text[..maxChars];
        return ToolResult<string>.Success(text);
    }

    public ToolResult<IReadOnlyList<CitationLink>> FindCitedCases(string caseId)
    {
        if (Find(caseId) is null)
        {
            return ToolResult<IReadOnlyList<CitationLink>>.Fail("Unknown case.");
        }

        var links = _links.Where(l => l.FromCaseId.Equals(caseId, StringComparison.OrdinalIgnoreCase)).ToList();
        return ToolResult<IReadOnlyList<CitationLink>>.Success(links);
    }

    public ToolResult<StatuteProvision> LookupStatute(string query) => StatuteCatalog.Lookup(query);

    public ToolResult<RetrievedPassage> GetPassage(string chunkId)
    {
        var passage = _index.Search(chunkId, "keyword", 20).FirstOrDefault(p => p.ChunkId.Equals(chunkId, StringComparison.OrdinalIgnoreCase));
        if (passage is not null)
        {
            return ToolResult<RetrievedPassage>.Success(passage);
        }

        foreach (var record in _records)
        {
            var chunk = TextChunker.Chunk(record).FirstOrDefault(c => c.ChunkId.Equals(chunkId, StringComparison.OrdinalIgnoreCase));
            if (chunk is null)
            {
                continue;
            }

            return ToolResult<RetrievedPassage>.Success(new RetrievedPassage
            {
                CaseId = record.CaseId,
                Citation = record.Citation,
                Title = record.Title,
                Court = record.Court,
                Year = record.Year,
                IsSynthetic = record.IsSynthetic,
                ChunkId = chunk.ChunkId,
                Start = chunk.Start,
                End = chunk.End,
                Text = chunk.Text[..Math.Min(420, chunk.Text.Length)],
                Score = 1,
                Method = "exact"
            });
        }

        return ToolResult<RetrievedPassage>.Fail("Unknown passage.");
    }

    public JudgmentRecord? Find(string caseId) =>
        _records.FirstOrDefault(r =>
            r.CaseId.Equals(caseId, StringComparison.OrdinalIgnoreCase) ||
            r.NeutralCitation.Equals(caseId, StringComparison.OrdinalIgnoreCase) ||
            r.Citation.Equals(caseId, StringComparison.OrdinalIgnoreCase) ||
            r.Path.Equals(caseId, StringComparison.OrdinalIgnoreCase));
}

public sealed class McpStdioServer
{
    private readonly LegalResearchLibrary _library;

    public McpStdioServer(LegalResearchLibrary library) => _library = library;

    public string Handle(string requestJson)
    {
        using var doc = JsonDocument.Parse(requestJson);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idElement) ? idElement.Clone() : default;
        var method = root.GetProperty("method").GetString();
        object? result = method switch
        {
            "initialize" => new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { tools = new { }, resources = new { }, prompts = new { } },
                serverInfo = new { name = "indian-legal-ai-lab", version = "0.1.0" }
            },
            "tools/list" => new { tools = ToolDefinitions() },
            "resources/list" => new
            {
                resources = new[]
                {
                    new { uri = "legal://statutes/article-21", name = "Article 21", mimeType = "text/plain" }
                }
            },
            "prompts/list" => new
            {
                prompts = new[]
                {
                    new { name = "case-brief", description = "Summarize a supplied judgment using only its text." }
                }
            },
            "tools/call" => Call(root.GetProperty("params")),
            _ => null
        };

        if (result is null)
        {
            return JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code = -32601, message = "Method not found." } });
        }

        return JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result });
    }

    private object Call(JsonElement p)
    {
        var name = p.GetProperty("name").GetString();
        var args = p.TryGetProperty("arguments", out var a) ? a : default;
        string Arg(string key) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out var v) ? v.ToString() : "";
        object payload = name switch
        {
            "search_cases" => _library.SearchCases(Arg("query"), string.IsNullOrEmpty(Arg("method")) ? "hybrid" : Arg("method")),
            "get_case_metadata" => _library.GetCaseMetadata(Arg("caseId")),
            "get_judgment" => _library.GetJudgment(Arg("caseId")),
            "find_cited_cases" => _library.FindCitedCases(Arg("caseId")),
            "lookup_statute" => _library.LookupStatute(Arg("query")),
            _ => ToolResult<string>.Fail("Tool is not on the allow list.")
        };
        return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(payload) } }, isError = false };
    }

    private static object[] ToolDefinitions() =>
    [
        Tool("search_cases", "Search judgments in the local corpus."),
        Tool("get_case_metadata", "Read normalized metadata for one case."),
        Tool("get_judgment", "Read extracted judgment text when it is present."),
        Tool("find_cited_cases", "List citation string matches. Does not decide overruling."),
        Tool("lookup_statute", "Look up a short constitutional provision in the local catalog.")
    ];

    private static object Tool(string name, string description) => new
    {
        name,
        description,
        inputSchema = new { type = "object", properties = new { query = new { type = "string" }, caseId = new { type = "string" }, method = new { type = "string" } } }
    };
}
