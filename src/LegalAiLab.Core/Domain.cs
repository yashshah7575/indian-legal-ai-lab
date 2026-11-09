namespace LegalAiLab.Core;

public static class ProductBoundary
{
    public const string Notice =
        "Educational legal information system. It does not provide legal advice, predict outcomes, or prepare filings.";
}

public sealed record JudgmentRecord
{
    public required string CaseId { get; init; }
    public required string Title { get; init; }
    public required string Citation { get; init; }
    public required string NeutralCitation { get; init; }
    public required string Court { get; init; }
    public required int Year { get; init; }
    public required string DecisionDate { get; init; }
    public required string Path { get; init; }
    public string? PdfObject { get; init; }
    public required string Petitioner { get; init; }
    public required string Respondent { get; init; }
    public required string Judge { get; init; }
    public required string DisposalNature { get; init; }
    public required bool IsSynthetic { get; init; }
    public required string SourceDataset { get; init; }
    public required string SourceLicense { get; init; }
    public required string Attribution { get; init; }
    public required string ExtractionStatus { get; init; }
    public string? Text { get; init; }
    public string? TextFile { get; init; }
}

public sealed record TextChunk
{
    public required string ChunkId { get; init; }
    public required string CaseId { get; init; }
    public required int Index { get; init; }
    public required string Text { get; init; }
    public required int Start { get; init; }
    public required int End { get; init; }
}

public sealed record RetrievedPassage
{
    public required string CaseId { get; init; }
    public required string Citation { get; init; }
    public required string Title { get; init; }
    public required string Court { get; init; }
    public required int Year { get; init; }
    public required bool IsSynthetic { get; init; }
    public required string ChunkId { get; init; }
    public required int Start { get; init; }
    public required int End { get; init; }
    public required string Text { get; init; }
    public required double Score { get; init; }
    public required string Method { get; init; }
}

public sealed record StatuteProvision
{
    public required string Id { get; init; }
    public required string Heading { get; init; }
    public required string Text { get; init; }
}

public sealed record CitationLink
{
    public required string FromCaseId { get; init; }
    public required string ToCaseId { get; init; }
    public required string MatchedText { get; init; }
    public required string Note { get; init; }
}

public sealed record CaseBrief
{
    public required string CaseId { get; init; }
    public required string Citation { get; init; }
    public required string Title { get; init; }
    public required bool IsSynthetic { get; init; }
    public required string Summary { get; init; }
    public required IReadOnlyList<string> SupportedByChunkIds { get; init; }
    public required IReadOnlyList<string> UnverifiedStatements { get; init; }
}

public sealed record ResearchReport
{
    public required string ReportId { get; init; }
    public required string Question { get; init; }
    public required string CreatedUtc { get; init; }
    public required IReadOnlyList<string> CasesConsidered { get; init; }
    public required IReadOnlyList<string> Findings { get; init; }
    public required IReadOnlyList<RetrievedPassage> Evidence { get; init; }
    public required IReadOnlyList<string> CompetingInterpretations { get; init; }
    public required IReadOnlyList<string> Limitations { get; init; }
    public required IReadOnlyList<string> References { get; init; }
    public required string HumanReview { get; init; }
}

public sealed record ToolResult<T>
{
    public required bool Ok { get; init; }
    public string? Error { get; init; }
    public T? Value { get; init; }

    public static ToolResult<T> Success(T value) => new() { Ok = true, Value = value };
    public static ToolResult<T> Fail(string error) => new() { Ok = false, Error = error };
}
