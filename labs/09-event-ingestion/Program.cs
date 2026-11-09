using LegalAiLab.Core;
using LegalAiLab.Ingestion;

public class Program
{
    public static void Main(string[] args)
    {
        var record = CorpusLoader.Load().First(r => r.CaseId == "1950 INSC 14");
        var pipeline = new IngestionPipeline();
        pipeline.Enqueue(new IngestionJob("thappar", record, 1));
        Console.WriteLine(pipeline.ProcessNext());
        Console.WriteLine("chunks " + pipeline.Chunks.Count);
    }
}
