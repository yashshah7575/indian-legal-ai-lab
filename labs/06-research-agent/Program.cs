using LegalAiLab.Agents;
using LegalAiLab.Mcp;

public class Program
{
    public static async Task Main(string[] args)
    {
        var agent = new ResearchAgent(new LegalResearchLibrary());
        var result = await agent.RunAsync("Romesh Thappar freedom of speech");
        Console.WriteLine(result.Outcome);
        Console.WriteLine(result.Brief?.Summary);
        foreach (var log in result.Logs) Console.WriteLine($"{log.Tool} ok={log.Ok} {log.Detail}");
    }
}
