using LegalAiLab.Workflows;
var dir = Path.Combine(Path.GetTempPath(), "lab10-sessions");
var store = new SessionStore(dir);
var state = new ResearchState { Question = "speech", Stage = "verify" };
await store.SaveAsync("demo", state);
var loaded = await store.LoadAsync("demo");
Console.WriteLine(loaded!.Stage);
store.Delete("demo");
Console.WriteLine(await store.LoadAsync("demo") is null);
