namespace Identity.Application.Administration;

/// <summary>
/// Batched natural-key lookups for the application catalog. Narrow on purpose, and batched because
/// bulk validation must resolve a whole file in a fixed number of queries, never one per row.
/// </summary>
public interface ICatalogCodeResolver
{
    Task<IReadOnlyDictionary<string, long>> FindApplicationIdsByCodes(
        IReadOnlyCollection<string> applicationCodes,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, long>> FindModuleIdsByCodes(
        long applicationId,
        IReadOnlyCollection<string> moduleCodes,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, long>> FindRoleIdsByCodes(
        long applicationId,
        IReadOnlyCollection<string> roleCodes,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, long>> FindCapabilityIdsByCodes(
        long applicationId,
        IReadOnlyCollection<string> capabilityCodes,
        CancellationToken cancellationToken);
}
