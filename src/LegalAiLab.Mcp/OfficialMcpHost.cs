using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegalAiLab.Mcp;

public static class OfficialMcpHost
{
    public static async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton<LegalResearchLibrary>();
        builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<LegalMcpTools>();
        await builder.Build().RunAsync(cancellationToken);
    }
}
