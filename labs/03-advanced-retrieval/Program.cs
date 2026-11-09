using LegalAiLab.Core;
using LegalAiLab.Retrieval;

public class Program
{
    public static void Main(string[] args)
    {
        var index = LegalIndex.FromRecords(CorpusLoader.Load());
        foreach (var method in new[] { "keyword", "vector", "hybrid" })
        {
            var hit = index.Search("Kharak Singh domiciliary visits", method, 1)[0];
            Console.WriteLine($"{method}: {hit.CaseId} score={hit.Score:0.000}");
        }

        Console.WriteLine(PgVectorSql.InitScript.Split('\n')[0]);
    }
}
