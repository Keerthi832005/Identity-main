using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed class AdministrationDiscoveryHandler(
    IAdministrationDiscoveryStore store,
    TimeProvider timeProvider) :
    IRequestHandler<GetAdministrationDashboardQuery, AdministrationDashboard>,
    IRequestHandler<SearchAdministrationApplicationsQuery, PagedAdministrationApplications>,
    IRequestHandler<SearchAdministrationUsersQuery, PagedAdministrationUsers>,
    IRequestHandler<GetAdministrationApplicationCatalogQuery, AdministrationApplicationCatalog>,
    IRequestHandler<SearchAdministrationApplicationUsersQuery, PagedAdministrationApplicationUsers>,
    IRequestHandler<GetAdministrationUserAccessQuery, AdministrationUserAccessCatalog>,
    IRequestHandler<GetAdministrationApplicationAccessQuery, AdministrationApplicationAccessCatalog>,
    IRequestHandler<GetAdministrationUserSecurityQuery, AdministrationUserSecurityCatalog>,
    IRequestHandler<GetAdministrationSecurityOperationsQuery, AdministrationSecurityOperations>
{
    public ValueTask<AdministrationDashboard> Handle(
        GetAdministrationDashboardQuery request,
        CancellationToken cancellationToken)
    {
        if (request.RecentLimit is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Recent limit must be between 1 and 20.");
        }

        return new ValueTask<AdministrationDashboard>(store.GetDashboard(
            request.RecentLimit,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken));
    }

    public ValueTask<PagedAdministrationApplications> Handle(
        SearchAdministrationApplicationsQuery request,
        CancellationToken cancellationToken)
    {
        ValidatePaging(request.Search, request.Skip, request.Take);
        return new ValueTask<PagedAdministrationApplications>(store.SearchApplications(
            Normalize(request.Search),
            request.Skip,
            request.Take,
            cancellationToken));
    }

    public ValueTask<PagedAdministrationUsers> Handle(
        SearchAdministrationUsersQuery request,
        CancellationToken cancellationToken)
    {
        ValidatePaging(request.Search, request.Skip, request.Take);
        return new ValueTask<PagedAdministrationUsers>(store.SearchUsers(
            Normalize(request.Search),
            request.Skip,
            request.Take,
            cancellationToken));
    }

    public async ValueTask<AdministrationApplicationCatalog> Handle(
        GetAdministrationApplicationCatalogQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ApplicationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        return await store.GetApplicationCatalog(request.ApplicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Application was not found.");
    }

    public async ValueTask<PagedAdministrationApplicationUsers> Handle(
        SearchAdministrationApplicationUsersQuery request,
        CancellationToken cancellationToken)
    {
        RequirePositive(request.ApplicationId, nameof(request));
        ValidatePaging(request.Search, request.Skip, request.Take);
        return await store.SearchApplicationUsers(
            request.ApplicationId,
            Normalize(request.Search),
            request.Skip,
            request.Take,
            cancellationToken)
            ?? throw new KeyNotFoundException("Application was not found.");
    }

    public async ValueTask<AdministrationUserAccessCatalog> Handle(
        GetAdministrationUserAccessQuery request,
        CancellationToken cancellationToken)
    {
        RequirePositive(request.UserId, nameof(request));
        return await store.GetUserAccessCatalog(
            request.UserId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken)
            ?? throw new KeyNotFoundException("User was not found.");
    }

    public async ValueTask<AdministrationApplicationAccessCatalog> Handle(
        GetAdministrationApplicationAccessQuery request,
        CancellationToken cancellationToken)
    {
        RequirePositive(request.ApplicationId, nameof(request));
        return await store.GetApplicationAccessCatalog(request.ApplicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Application was not found.");
    }

    public async ValueTask<AdministrationUserSecurityCatalog> Handle(
        GetAdministrationUserSecurityQuery request,
        CancellationToken cancellationToken)
    {
        RequirePositive(request.UserId, nameof(request));
        return await store.GetUserSecurityCatalog(
            request.UserId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken)
            ?? throw new KeyNotFoundException("User was not found.");
    }

    public ValueTask<AdministrationSecurityOperations> Handle(
        GetAdministrationSecurityOperationsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Skip < 0 || request.Take is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.EventType?.Length > 100 || request.From > request.To) throw new ArgumentOutOfRangeException(nameof(request));
        return new ValueTask<AdministrationSecurityOperations>(store.GetSecurityOperations(
            request with { EventType = string.IsNullOrWhiteSpace(request.EventType) ? null : request.EventType.Trim() },
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken));
    }

    private static void ValidatePaging(string? search, int skip, int take)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(take));
        if (search?.Trim().Length > 100) throw new ArgumentOutOfRangeException(nameof(search));
    }

    private static string? Normalize(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    private static void RequirePositive(long value, string name)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(name);
    }
}
