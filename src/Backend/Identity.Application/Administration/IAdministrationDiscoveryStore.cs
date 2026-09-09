namespace Identity.Application.Administration;

public interface IAdministrationDiscoveryStore
{
    Task<AdministrationDashboard> GetDashboard(
        int recentLimit,
        DateTime generatedAt,
        CancellationToken cancellationToken);

    Task<PagedAdministrationApplications> SearchApplications(
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<PagedAdministrationUsers> SearchUsers(
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<AdministrationApplicationCatalog?> GetApplicationCatalog(
        long applicationId,
        CancellationToken cancellationToken);

    Task<PagedAdministrationApplicationUsers?> SearchApplicationUsers(
        long applicationId,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<AdministrationUserAccessCatalog?> GetUserAccessCatalog(
        long userId,
        DateTime evaluatedAt,
        CancellationToken cancellationToken);

    Task<AdministrationApplicationAccessCatalog?> GetApplicationAccessCatalog(
        long applicationId,
        CancellationToken cancellationToken);

    Task<AdministrationUserSecurityCatalog?> GetUserSecurityCatalog(
        long userId,
        DateTime evaluatedAt,
        CancellationToken cancellationToken);

    Task<AdministrationSecurityOperations> GetSecurityOperations(
        GetAdministrationSecurityOperationsQuery query,
        DateTime generatedAt,
        CancellationToken cancellationToken);
}
