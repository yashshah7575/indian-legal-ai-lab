using LegalAiLab.Core;

var client = new GroundedChatClient();
var records = CorpusLoader.Load();
var thappar = records.First(r => r.CaseId == "1950 INSC 14");
var excerpt = thappar.Text![..400].Replace('\n', ' ');
var passage = "PASSAGE: " + thappar.Citation + " " + excerpt;
var grounded = await client.GetResponseAsync([new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "Summarize.\n" + passage)]);
var bare = await client.GetResponseAsync([new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "Summarize the judgment.")]);
Console.WriteLine(grounded.Text);
Console.WriteLine("---");
Console.WriteLine(bare.Text);
