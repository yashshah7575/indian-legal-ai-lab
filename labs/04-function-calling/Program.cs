using LegalAiLab.Mcp;

public class Program
{
    public static void Main(string[] args)
    {
        var lib = new LegalResearchLibrary();
        var meta = lib.GetCaseMetadata("1950 INSC 14");
        var statute = lib.LookupStatute("Article 21");
        Console.WriteLine(meta.Value!.Title);
        Console.WriteLine(statute.Value!.Text);
        Console.WriteLine(lib.SearchCases("").Error);
    }
}
