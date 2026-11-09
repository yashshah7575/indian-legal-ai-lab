using System.Text.Json;

namespace LegalAiLab.Core;

public static class SampleDataLocator
{
    public static string FindManifest()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "sample-data", "manifest.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("sample-data/manifest.json was not found by walking up from the application base directory.");
    }
}

public sealed class CorpusManifest
{
    public required string DatasetVersion { get; init; }
    public string? Notes { get; init; }
    public required List<ManifestRecord> Records { get; init; }
}

public sealed class ManifestRecord
{
    public string? SourceDataset { get; init; }
    public string? SourceLicense { get; init; }
    public string? Attribution { get; init; }
    public bool IsSynthetic { get; init; }
    public string? Court { get; init; }
    public int Year { get; init; }
    public string? Path { get; init; }
    public string? PdfObject { get; init; }
    public string? Title { get; init; }
    public string? Petitioner { get; init; }
    public string? Respondent { get; init; }
    public string? Citation { get; init; }
    public string? CaseId { get; init; }
    public string? NeutralCitation { get; init; }
    public string? DecisionDate { get; init; }
    public string? DisposalNature { get; init; }
    public string? Judge { get; init; }
    public string? TextFile { get; init; }
    public string? ExtractionStatus { get; init; }
}

public static class CorpusLoader
{
    public static IReadOnlyList<JudgmentRecord> Load(string? manifestPath = null)
    {
        manifestPath ??= SampleDataLocator.FindManifest();
        var json = File.ReadAllText(manifestPath);
        var expected = File.ReadAllText(Path.Combine(Path.GetDirectoryName(manifestPath)!, "manifest.sha256")).Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(manifestPath))).ToLowerInvariant();
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("sample-data/manifest.json does not match manifest.sha256.");
        }

        var manifest = JsonSerializer.Deserialize<CorpusManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Manifest was empty.");
        var root = Path.GetDirectoryName(manifestPath)!;
        var records = new List<JudgmentRecord>();
        foreach (var item in manifest.Records)
        {
            string? text = null;
            if (!string.IsNullOrWhiteSpace(item.TextFile))
            {
                var full = Path.Combine(root, item.TextFile.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full))
                {
                    text = File.ReadAllText(full);
                }
            }

            records.Add(new JudgmentRecord
            {
                CaseId = item.CaseId ?? item.NeutralCitation ?? item.Path ?? "unknown",
                Title = item.Title ?? "",
                Citation = item.Citation ?? "",
                NeutralCitation = item.NeutralCitation ?? "",
                Court = item.Court ?? "",
                Year = item.Year,
                DecisionDate = item.DecisionDate ?? "",
                Path = item.Path ?? "",
                PdfObject = item.PdfObject,
                Petitioner = item.Petitioner ?? "",
                Respondent = item.Respondent ?? "",
                Judge = item.Judge ?? "",
                DisposalNature = item.DisposalNature ?? "",
                IsSynthetic = item.IsSynthetic,
                SourceDataset = item.SourceDataset ?? "",
                SourceLicense = item.SourceLicense ?? "",
                Attribution = item.Attribution ?? "",
                ExtractionStatus = text is null ? (item.ExtractionStatus ?? "missing_text") : "extracted",
                Text = text,
                TextFile = item.TextFile
            });
        }

        return records;
    }
}

public static class StatuteCatalog
{
    public static IReadOnlyList<StatuteProvision> Provisions { get; } =
    [
        new StatuteProvision
        {
            Id = "constitution:article-19-1-a",
            Heading = "Constitution of India, Article 19(1)(a)",
            Text = "All citizens shall have the right to freedom of speech and expression."
        },
        new StatuteProvision
        {
            Id = "constitution:article-19-2",
            Heading = "Constitution of India, Article 19(2)",
            Text = "Article 19(2) allows the State to make law imposing reasonable restrictions on the right in Article 19(1)(a) in the interests of the sovereignty and integrity of India, the security of the State, friendly relations with foreign States, public order, decency or morality, or in relation to contempt of court, defamation or incitement to an offence."
        },
        new StatuteProvision
        {
            Id = "constitution:article-21",
            Heading = "Constitution of India, Article 21",
            Text = "No person shall be deprived of his life or personal liberty except according to procedure established by law."
        }
    ];

    public static ToolResult<StatuteProvision> Lookup(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResult<StatuteProvision>.Fail("A statute query is required.");
        }

        var hit = Provisions.FirstOrDefault(p =>
            p.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Heading.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Text.Contains(query, StringComparison.OrdinalIgnoreCase));
        return hit is null
            ? ToolResult<StatuteProvision>.Fail("No statute provision in the local catalog matched that query.")
            : ToolResult<StatuteProvision>.Success(hit);
    }
}

public static class AdviceGuard
{
    public static bool IsDisallowed(string question)
    {
        var q = question.ToLowerInvariant();
        string[] blocked = ["will i win", "will we win", "chance of winning", "draft a petition", "file this case", "legal advice for me"];
        return blocked.Any(q.Contains);
    }
}
