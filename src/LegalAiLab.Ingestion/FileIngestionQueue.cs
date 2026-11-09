using System.Text.Json;

namespace LegalAiLab.Ingestion;

public sealed class FileIngestionQueue
{
    private readonly string _root;
    private readonly object _gate = new();

    public FileIngestionQueue(string root)
    {
        _root = root;
        Directory.CreateDirectory(Path.Combine(root, "pending"));
        Directory.CreateDirectory(Path.Combine(root, "done"));
        Directory.CreateDirectory(Path.Combine(root, "dead"));
    }

    public void Enqueue(IngestionJob job)
    {
        var path = Path.Combine(_root, "pending", Safe(job.JobId) + ".json");
        if (File.Exists(Path.Combine(_root, "done", Safe(job.JobId) + ".json")))
        {
            return;
        }

        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(job));
        File.Move(temp, path, overwrite: true);
    }

    public IngestionOutcome ProcessNext(IngestionPipeline pipeline, int maxAttempts = 3)
    {
        lock (_gate)
        {
            var pending = Directory.GetFiles(Path.Combine(_root, "pending"), "*.json").OrderBy(path => path).FirstOrDefault();
            if (pending is null)
            {
                throw new InvalidOperationException("The durable queue is empty.");
            }

            var job = JsonSerializer.Deserialize<IngestionJob>(File.ReadAllText(pending))
                ?? throw new InvalidDataException("Job file was empty.");
            pipeline.Enqueue(job);
            var outcome = pipeline.ProcessNext(maxAttempts);
            var name = Path.GetFileName(pending);
            var destDir = outcome.Status == "dead-letter" ? "dead" : "done";
            File.Move(pending, Path.Combine(_root, destDir, name), overwrite: true);
            return outcome;
        }
    }

    private static string Safe(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId) || jobId.Any(ch => !char.IsLetterOrDigit(ch) && ch != '-'))
        {
            throw new ArgumentException("Job id must be letters, digits, or hyphens.");
        }

        return jobId;
    }
}
