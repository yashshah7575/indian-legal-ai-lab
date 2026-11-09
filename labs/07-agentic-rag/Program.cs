using LegalAiLab.Mcp;
using LegalAiLab.Workflows;
var state = await new ResearchWorkflow(new LegalResearchLibrary()).RunAsync("privacy personal liberty Kharak Singh");
Console.WriteLine(string.Join(", ", state.Report!.CasesConsidered));
Console.WriteLine(state.Report.HumanReview);
