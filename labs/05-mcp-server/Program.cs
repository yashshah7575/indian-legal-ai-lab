using LegalAiLab.Mcp;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Primary server: dotnet run --project src/LegalAiLab.McpHost");
        Console.WriteLine("The lines below are the teaching JSON handler, not the SDK transport.");
        var server = new McpStdioServer(new LegalResearchLibrary());
        Console.WriteLine(server.Handle("""{"jsonrpc":"2.0","id":1,"method":"initialize"}"""));
        Console.WriteLine(server.Handle("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""));
    }
}
