using LegalAiLab.Mcp;
using LegalAiLab.Workflows;

public class Program
{
    public static async Task Main(string[] args)
    {
        var state = await new ResearchWorkflow(new LegalResearchLibrary()).RunAsync("privacy personal liberty Kharak Singh");
        Console.WriteLine(string.Join(", ", state.Report!.CasesConsidered));
        Console.WriteLine(state.Report.HumanReview);
    }
}
