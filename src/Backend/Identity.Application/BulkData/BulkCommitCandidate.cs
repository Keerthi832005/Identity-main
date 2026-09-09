namespace Identity.Application.BulkData;

public sealed record BulkCommitCandidate(
    int SourceRowNumber,
    IReadOnlyDictionary<string, string?> Values);
