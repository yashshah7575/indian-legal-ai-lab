using LegalAiLab.Mcp;
using LegalAiLab.Workflows;

public class Program
{
    public static async Task Main(string[] args)
    {
        var state = await new ResearchWorkflow(new LegalResearchLibrary()).RunAsync("Article 19 speech Romesh Thappar");
        Console.WriteLine(state.Stage);
        foreach (var note in state.VerificationNotes) Console.WriteLine(note);
    }
}
