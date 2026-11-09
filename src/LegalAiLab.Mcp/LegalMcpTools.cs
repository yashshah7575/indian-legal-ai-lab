using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace LegalAiLab.Mcp;

[McpServerToolType]
public sealed class LegalMcpTools
{
    private readonly LegalResearchLibrary _library;

    public LegalMcpTools(LegalResearchLibrary library) => _library = library;

    [McpServerTool(Name = "search_cases"), Description("Search the local judgment corpus.")]
    public string SearchCases(string query) => JsonSerializer.Serialize(_library.SearchCases(query));

    [McpServerTool(Name = "get_case_metadata"), Description("Read metadata for one case.")]
    public string GetCaseMetadata(string caseId) => JsonSerializer.Serialize(_library.GetCaseMetadata(caseId));

    [McpServerTool(Name = "get_judgment"), Description("Read extracted judgment text when it is stored.")]
    public string GetJudgment(string caseId) => JsonSerializer.Serialize(_library.GetJudgment(caseId));

    [McpServerTool(Name = "find_cited_cases"), Description("List citation string matches. Does not decide overruling.")]
    public string FindCitedCases(string caseId) => JsonSerializer.Serialize(_library.FindCitedCases(caseId));

    [McpServerTool(Name = "lookup_statute"), Description("Look up a short constitutional provision.")]
    public string LookupStatute(string query) => JsonSerializer.Serialize(_library.LookupStatute(query));
}
