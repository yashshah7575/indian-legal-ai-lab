using LegalAiLab.Core;
using LegalAiLab.Retrieval;

public class Program
{
    public static void Main(string[] args)
    {
        var index = LegalIndex.FromRecords(CorpusLoader.Load());
        foreach (var hit in index.Search("freedom of speech Romesh Thappar", "vector", 3))
            Console.WriteLine($"{hit.Score:0.000} {hit.CaseId} {hit.ChunkId}");
    }
}
