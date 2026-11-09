using LegalAiLab.Core;
using LegalAiLab.Retrieval;
var index = LegalIndex.FromRecords(CorpusLoader.Load());
foreach (var hit in index.Search("freedom of speech Romesh Thappar", "vector", 3))
    Console.WriteLine($"{hit.Score:0.000} {hit.CaseId} {hit.ChunkId}");
