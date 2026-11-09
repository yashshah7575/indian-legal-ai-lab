using LegalAiLab.Mcp;

public class Program
{
    public static void Main(string[] args)
    {
        var server = new McpStdioServer(new LegalResearchLibrary());
        Console.WriteLine(server.Handle("""{"jsonrpc":"2.0","id":1,"method":"initialize"}"""));
        Console.WriteLine(server.Handle("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""));
    }
}
