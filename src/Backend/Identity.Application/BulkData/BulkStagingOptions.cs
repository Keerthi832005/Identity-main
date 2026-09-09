namespace Identity.Application.BulkData;

/// <summary>
/// Staged rows hold administrator-supplied identity data, so retention is bounded rather than
/// indefinite: an abandoned batch expires instead of accumulating.
/// </summary>
public sealed record BulkStagingOptions(TimeSpan Retention, BulkDocumentLimits Limits)
{
    public static BulkStagingOptions Default { get; } =
        new(TimeSpan.FromHours(24), BulkDocumentLimits.Default);
}
