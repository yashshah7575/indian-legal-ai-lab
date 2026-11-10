using System.Diagnostics;
using LegalAiLab.Core;
using LegalAiLab.Ingestion;
using LegalAiLab.Mcp;
using LegalAiLab.Workflows;
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddHealthChecks();
        builder.Services.AddSingleton<LegalResearchLibrary>();
        builder.Services.AddSingleton<ResearchWorkflow>();
        builder.Services.AddSingleton(new SessionStore(Path.Combine(builder.Environment.ContentRootPath, "data", "sessions")));
        builder.Services.AddSingleton<IngestionPipeline>();
        builder.Services.AddSingleton(new FileIngestionQueue(Path.Combine(builder.Environment.ContentRootPath, "data", "ingestion")));
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenTelemetry().WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation();
            tracing.AddSource("LegalAiLab");
        });

        var app = builder.Build();
        var activity = new ActivitySource("LegalAiLab");
        app.MapHealthChecks("/health");
        app.MapGet("/", () => Results.Ok(new { name = "indian-legal-ai-lab", notice = ProductBoundary.Notice }));

        app.MapGet("/cases", (string? q, string? court, int? year, LegalResearchLibrary library) =>
        {
            using var span = activity.StartActivity("search");
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.Ok(library.Records.Select(r => new { r.CaseId, r.Title, r.Citation, r.Year, r.IsSynthetic, r.ExtractionStatus }));
            }

            var filters = new LegalAiLab.Retrieval.SearchFilters { Court = court, Year = year };
            return Results.Ok(library.SearchCases(q, "hybrid", 8, filters));
        });

        app.MapGet("/cases/{id}", (string id, LegalResearchLibrary library) =>
        {
            var meta = library.GetCaseMetadata(id);
            return meta.Ok ? Results.Ok(meta) : Results.NotFound(meta);
        });

        app.Use(async (context, next) =>
        {
            var token = builder.Configuration["Api:Token"];
            var path = context.Request.Path.Value ?? "";
            var sensitive = HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method);
            if (!string.IsNullOrWhiteSpace(token) && sensitive && path is not "/health")
            {
                if (context.Request.Headers["X-Api-Key"] != token)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }

            await next();
        });
        app.MapPost("/research", async (ResearchRequest request, ResearchWorkflow workflow, SessionStore sessions, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["question"] = ["A question is required."] });
            }

            cancellationToken.ThrowIfCancellationRequested();
            var state = await workflow.RunAsync(request.Question);
            var id = Guid.NewGuid().ToString("n");
            await sessions.SaveAsync(id, state);
            return Results.Ok(new { sessionId = id, state.Stage, state.Report });
        });

        app.MapGet("/sessions/{id}", async (string id, SessionStore sessions) =>
        {
            var state = await sessions.LoadAsync(id);
            return state is null ? Results.NotFound() : Results.Ok(state);
        });

        app.MapDelete("/sessions/{id}", (string id, SessionStore sessions) =>
        {
            sessions.Delete(id);
            return Results.NoContent();
        });

        app.MapPost("/ingest", (IngestRequest request, LegalResearchLibrary library, IngestionPipeline pipeline, FileIngestionQueue queue) =>
        {
            if (string.IsNullOrWhiteSpace(request.CaseId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["caseId"] = ["A case id is required."] });
            }

            var record = library.Find(request.CaseId);
            if (record is null)
            {
                return Results.NotFound(new { error = "Case is not in the local manifest." });
            }

            var jobId = request.JobId ?? record.CaseId;
            queue.Enqueue(new IngestionJob(jobId, record, request.FailTimes));
            var outcome = queue.ProcessNext(pipeline);
            return Results.Ok(outcome);
        });

        app.MapGet("/citations/{id}", (string id, LegalResearchLibrary library) => library.FindCitedCases(id));

        app.Run();

    }
}

public sealed record ResearchRequest(string Question);
public sealed record IngestRequest(string CaseId, string? JobId, int FailTimes = 0);
