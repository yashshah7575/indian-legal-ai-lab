using LegalAiLab.Mcp;
using LegalAiLab.Workflows;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddRazorComponents();
        builder.Services.AddSingleton<LegalResearchLibrary>();
        builder.Services.AddSingleton<ResearchWorkflow>();
        builder.Services.AddSingleton(new SessionStore(Path.Combine(builder.Environment.ContentRootPath, "data", "sessions")));
        builder.Services.AddHealthChecks();
        var app = builder.Build();
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapHealthChecks("/health");
        app.MapRazorComponents<LegalAiLab.Web.Components.App>();
        app.Run();
    }
}
