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

public class ProviderAndDurabilityTests
{
    [Fact]
    public void Bm25_finds_kharak_singh()
    {
        var index = LegalIndex.FromRecords(CorpusLoader.Load());
        var hits = index.Search("Kharak Singh domiciliary visits", "bm25", 1);
        Assert.Equal("1962 INSC 389", hits[0].CaseId);
    }

    [Fact]
    public async Task OpenAiCompatible_client_reads_choices()
    {
        var handler = new StubHandler("""{"choices":[{"message":{"content":"from-provider"}}]}""");
        var client = new OpenAiCompatibleChatClient(new HttpClient(handler), new ModelProviderOptions { ChatBaseUrl = "http://localhost:11434/", ChatModel = "test" });
        var response = await client.GetResponseAsync([new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "hello")]);
        Assert.Equal("from-provider", response.Text);
    }

    [Fact]
    public void Durable_queue_skips_a_completed_job_id()
    {
        var root = Path.Combine(Path.GetTempPath(), "legal-queue-" + Guid.NewGuid().ToString("n"));
        var queue = new FileIngestionQueue(root);
        var record = CorpusLoader.Load().First(r => r.Text is not null);
        var pipeline = new IngestionPipeline();
        queue.Enqueue(new IngestionJob("job-1", record, 0));
        Assert.Equal("completed", queue.ProcessNext(pipeline).Status);
        queue.Enqueue(new IngestionJob("job-1", record, 0));
        Assert.False(File.Exists(Path.Combine(root, "pending", "job-1.json")));
    }

    [Fact]
    public async Task Session_id_rejects_path_traversal()
    {
        var store = new SessionStore(Path.Combine(Path.GetTempPath(), "legal-sess-" + Guid.NewGuid().ToString("n")));
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync("../etc", new ResearchState { Question = "q" }));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        public StubHandler(string body) => _body = body;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(_body) });
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
    public async Task Agent_budget_resets_between_runs()
    {
        var agent = new ResearchAgent(new LegalResearchLibrary(), maxCalls: 3);
        var first = await agent.RunAsync("Romesh Thappar freedom of speech");
        var second = await agent.RunAsync("Romesh Thappar freedom of speech");
        Assert.Equal("completed", first.Outcome);
        Assert.Equal("completed", second.Outcome);
    }

    [Fact]
    public async Task Agent_executes_a_model_selected_tool()
    {
        var chat = new SelectingChat();
        var agent = new ResearchAgent(new LegalResearchLibrary(), chat: chat);
        var result = await agent.RunAsync("speech");
        Assert.Contains(result.Logs, log => log.Tool == "search_cases" && log.Ok);
        Assert.Equal("completed", result.Outcome);
    }

    [Fact]
    public void Citation_existence_is_not_passage_support()
    {
        var catalog = CorpusLoader.Load();
        var findings = CitationChecker.Assess("[1950] 1 S.C.R. 594", catalog, ["no citation in this passage"]);
        Assert.True(findings[0].ExistsInCatalog);
        Assert.False(findings[0].SupportedByPassage);
    }

    [Fact]
    public async Task Embedding_without_a_provider_is_labeled_offline()
    {
        var result = await new EmbeddingService().EmbedAsync("freedom of speech");
        Assert.False(result.IsSemantic);
        Assert.Equal("hash-offline", result.Provider);
        Assert.Equal(64, result.Vector.Length);
    }

    [Fact]
    public async Task Chat_client_streams_deltas_and_reads_tool_calls()
    {
        var stream = new StreamHandler("data: {\"choices\":[{\"delta\":{\"content\":\"ab\"}}]}\n\ndata: [DONE]\n");
        var client = new OpenAiCompatibleChatClient(new HttpClient(stream), new ModelProviderOptions { ChatBaseUrl = "http://localhost:9/", ChatModel = "m" });
        var parts = new List<string?>();
        await foreach (var update in client.GetStreamingResponseAsync([new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "q")]))
        {
            parts.Add(update.Text);
        }

        Assert.Contains("ab", parts);
        var tools = new ToolHandler("""{"choices":[{"message":{"content":"","tool_calls":[{"id":"1","function":{"name":"search_cases","arguments":"{\"query\":\"speech\"}"}}]}}]}""");
        var toolClient = new OpenAiCompatibleChatClient(new HttpClient(tools), new ModelProviderOptions { ChatBaseUrl = "http://localhost:9/", ChatModel = "m" });
        var response = await toolClient.GetResponseAsync([new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "q")]);
        Assert.Contains(response.Messages.SelectMany(message => message.Contents), content => content is Microsoft.Extensions.AI.FunctionCallContent);
    }

    [Fact]
    public async Task Mcp_stdio_client_lists_and_calls_search()
    {
        var project = FindProject("src/LegalAiLab.McpHost/LegalAiLab.McpHost.csproj");
        var transport = new ModelContextProtocol.Client.StdioClientTransport(new ModelContextProtocol.Client.StdioClientTransportOptions
        {
            Command = "dotnet",
            Arguments = ["run", "--project", project, "--no-build"]
        });
        await using var client = await ModelContextProtocol.Client.McpClient.CreateAsync(transport);
        var tools = await client.ListToolsAsync();
        Assert.Contains(tools, tool => tool.Name == "search_cases");
        var called = await client.CallToolAsync("lookup_statute", new Dictionary<string, object?> { ["query"] = "Article 21" });
        Assert.Contains("Article 21", System.Text.Json.JsonSerializer.Serialize(called), StringComparison.OrdinalIgnoreCase);
    }

    private static string FindProject(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    private sealed class SelectingChat : Microsoft.Extensions.AI.IChatClient
    {
        private int _calls;
        public Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, Microsoft.Extensions.AI.ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            _calls++;
            if (_calls == 1)
            {
                var message = new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, "");
                message.Contents.Add(new Microsoft.Extensions.AI.FunctionCallContent("1", "search_cases", new Dictionary<string, object?> { ["query"] = "Romesh Thappar freedom of speech" }));
                return Task.FromResult(new Microsoft.Extensions.AI.ChatResponse(message));
            }

            return new GroundedChatClient().GetResponseAsync(messages, options, cancellationToken);
        }

        public IAsyncEnumerable<Microsoft.Extensions.AI.ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, Microsoft.Extensions.AI.ChatOptions? options = null, CancellationToken cancellationToken = default)
            => new GroundedChatClient().GetStreamingResponseAsync(messages, options, cancellationToken);
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class StreamHandler : HttpMessageHandler
    {
        private readonly string _body;
        public StreamHandler(string body) => _body = body;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(_body) });
    }

    private sealed class ToolHandler : HttpMessageHandler
    {
        private readonly string _body;
        public ToolHandler(string body) => _body = body;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json") });
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
