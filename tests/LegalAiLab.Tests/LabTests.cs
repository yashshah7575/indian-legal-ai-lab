using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LegalAiLab.Agents;
using LegalAiLab.Core;
using LegalAiLab.Ingestion;
using LegalAiLab.Mcp;
using LegalAiLab.Retrieval;
using LegalAiLab.Workflows;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LegalAiLab.Tests;

public class CorpusTests
{
    [Fact]
    public void Manifest_checksum_matches_and_real_cases_are_labeled()
    {
        var records = CorpusLoader.Load();
        Assert.Contains(records, r => r.NeutralCitation == "1950INSC14" && !r.IsSynthetic && r.Text is not null);
        Assert.Contains(records, r => r.CaseId == "1962 INSC 389" && r.Text is not null);
        Assert.Contains(records, r => r.CaseId == "2017 INSC 801" && r.Text is null);
        Assert.All(records.Where(r => r.IsSynthetic), r => Assert.Contains("FICTIONAL", r.Title));
    }

    [Fact]
    public void Duplicate_detector_flags_identical_identity()
    {
        var records = CorpusLoader.Load().Take(1).ToList();
        var copy = records[0] with { CaseId = records[0].CaseId + "-copy" };
        var dupes = DuplicateDetector.DuplicateCaseIds([records[0], copy]);
        Assert.Equal(2, dupes.Count);
    }
}

public class RetrievalTests
{
    [Fact]
    public void Hybrid_search_finds_thappar_for_speech_query()
    {
        var index = LegalIndex.FromRecords(CorpusLoader.Load());
        var hits = index.Search("Romesh Thappar freedom of speech Madras", "hybrid", 3);
        Assert.Equal("1950 INSC 14", hits[0].CaseId);
        Assert.False(hits[0].IsSynthetic);
    }

    [Fact]
    public void Year_filter_excludes_other_years()
    {
        var index = LegalIndex.FromRecords(CorpusLoader.Load());
        var hits = index.Search("freedom speech", "keyword", 5, new SearchFilters { Year = 2024 });
        Assert.All(hits, h => Assert.Equal(2024, h.Year));
    }

    [Fact]
    public void Pgvector_sql_mentions_filters()
    {
        Assert.Contains("vector", PgVectorSql.InitScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@year", PgVectorSql.SearchSql);
    }
}

public class ToolAndMcpTests
{
    [Fact]
    public void Tools_reject_blank_input_and_unknown_tools()
    {
        var lib = new LegalResearchLibrary();
        Assert.False(lib.SearchCases(" ").Ok);
        Assert.False(lib.GetCaseMetadata("no-such-case").Ok);
        Assert.True(lib.LookupStatute("Article 21").Ok);
        var server = new McpStdioServer(lib);
        using var doc = JsonDocument.Parse(server.Handle("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"delete_corpus\",\"arguments\":{}}}"));
        var text = doc.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();
        Assert.Contains("allow list", text);
    }

    [Fact]
    public void Mcp_lists_read_only_tools()
    {
        var server = new McpStdioServer(new LegalResearchLibrary());
        var json = server.Handle("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}");
        foreach (var name in new[] { "search_cases", "get_case_metadata", "get_judgment", "find_cited_cases", "lookup_statute" })
        {
            Assert.Contains(name, json);
        }
    }

    [Fact]
    public void Synthetic_citation_link_does_not_claim_overruling()
    {
        var lib = new LegalResearchLibrary();
        var links = lib.FindCitedCases("SYN-ART19-001");
        Assert.True(links.Ok);
        Assert.Contains(links.Value!, l => l.ToCaseId == "SYN-ART21-001");
        Assert.All(links.Value!, l => Assert.Contains("does not mean", l.Note));
    }
}

public class AgentWorkflowTests
{
    [Fact]
    public async Task Agent_refuses_outcome_prediction()
    {
        var agent = new ResearchAgent(new LegalResearchLibrary());
        var result = await agent.RunAsync("Will I win my case?");
        Assert.Equal("refused-advice-or-prediction", result.Outcome);
    }

    [Fact]
    public async Task Agent_brief_is_grounded_in_passages()
    {
        var agent = new ResearchAgent(new LegalResearchLibrary());
        var result = await agent.RunAsync("What does the extracted text say about Romesh Thappar and freedom of speech?");
        Assert.True(result.Ok);
        Assert.Contains("1950 INSC 14", result.Brief!.Summary);
        Assert.Empty(result.Brief.UnverifiedStatements);
        Assert.True(result.ToolCalls <= 4);
    }

    [Fact]
    public async Task Ungrounded_client_does_not_invent_a_known_holding()
    {
        var client = new GroundedChatClient();
        var response = await client.GetResponseAsync([new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "Summarize the judgment.")]);
        Assert.Contains("no source passages", response.Text, StringComparison.OrdinalIgnoreCase);
        var unknown = CitationChecker.UnknownCitations(response.Text!, CorpusLoader.Load());
        Assert.Contains("FICTIONAL 2099 SYN 5", unknown);
    }

    [Fact]
    public async Task Workflow_reports_limitations_for_privacy_question()
    {
        var workflow = new ResearchWorkflow(new LegalResearchLibrary());
        var state = await workflow.RunAsync("How do the available materials discuss privacy and personal liberty?");
        Assert.Equal("completed", state.Stage);
        Assert.Contains(state.Report!.CasesConsidered, id => id == "1962 INSC 389");
        Assert.Contains(state.Report.Limitations, l => l.Contains("does not", StringComparison.OrdinalIgnoreCase) || l.Contains("string matches"));
    }

    [Fact]
    public async Task Session_can_resume_and_be_deleted()
    {
        var root = Path.Combine(Path.GetTempPath(), "legal-ai-lab-" + Guid.NewGuid().ToString("n"));
        var store = new SessionStore(root);
        var state = new ResearchState { Question = "speech", Stage = "analyze", Discovered = [] };
        await store.SaveAsync("s1", state);
        var loaded = await store.LoadAsync("s1");
        Assert.Equal("analyze", loaded!.Stage);
        var workflow = new ResearchWorkflow(new LegalResearchLibrary());
        var resumed = await workflow.RunAsync("Romesh Thappar freedom of speech", loaded);
        Assert.Equal("completed", resumed.Stage);
        store.Delete("s1");
        Assert.Null(await store.LoadAsync("s1"));
    }
}

public class IngestionTests
{
    [Fact]
    public void Pipeline_retries_then_dead_letters()
    {
        var pipeline = new IngestionPipeline();
        var record = CorpusLoader.Load().First(r => r.Text is not null);
        pipeline.Enqueue(new IngestionJob("job-fail", record, FailTimes: 5));
        var outcome = pipeline.ProcessNext(maxAttempts: 2);
        Assert.Equal("dead-letter", outcome.Status);
        Assert.Single(pipeline.DeadLetters);
    }

    [Fact]
    public void Pipeline_is_idempotent_and_marks_incomplete_text()
    {
        var pipeline = new IngestionPipeline();
        var record = CorpusLoader.Load().First(r => r.Text is not null);
        pipeline.Enqueue(new IngestionJob("job-ok", record, 0));
        Assert.Equal("completed", pipeline.ProcessNext().Status);
        pipeline.Enqueue(new IngestionJob("job-ok", record, 0));
        Assert.Equal("idempotent-skip", pipeline.ProcessNext().Status);
        var missing = record with { Text = null, ExtractionStatus = "metadata_only_pdf_not_vendored" };
        pipeline.Enqueue(new IngestionJob("job-missing", missing, 0));
        Assert.Equal("incomplete-document", pipeline.ProcessNext().Status);
    }
}

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_and_search_work()
    {
        var client = _factory.CreateClient();
        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        var search = await client.GetAsync("/cases?q=Romesh%20Thappar%20speech");
        var body = await search.Content.ReadAsStringAsync();
        Assert.Contains("1950 INSC 14", body);
    }

    [Fact]
    public async Task Research_refuses_prediction()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/research", new ResearchRequest("Will I win my case?"));
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("refused", json);
    }
}
