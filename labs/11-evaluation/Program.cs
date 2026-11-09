using LegalAiLab.Core;
using LegalAiLab.Retrieval;

public class Program
{
    public static void Main(string[] args)
    {
        var index = LegalIndex.FromRecords(CorpusLoader.Load());
        var hit = index.Search("Romesh Thappar freedom of speech Madras", "hybrid", 1)[0];
        Console.WriteLine(hit.CaseId == "1950 INSC 14" ? "recall_at_1=1 for the fixed speech query" : "recall_at_1=0");
        var unknown = CitationChecker.UnknownCitations("FICTIONAL 2099 SYN 5", CorpusLoader.Load());
        Console.WriteLine("unknown_citations=" + unknown.Count);
    }
}
