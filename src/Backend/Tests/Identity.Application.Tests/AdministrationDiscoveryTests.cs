using Identity.Application.Administration;

namespace Identity.Application.Tests;

public sealed class AdministrationDiscoveryTests
{
    [Fact]
    public async Task Dashboard_UsesBoundedLimitAndTimeProvider()
    {
        var now = new DateTimeOffset(2026, 8, 29, 12, 30, 0, TimeSpan.Zero);
        var store = new DiscoveryStore();
        var handler = new AdministrationDiscoveryHandler(store, new FixedTimeProvider(now));

        var result = await handler.Handle(
            new GetAdministrationDashboardQuery(7),
            TestContext.Current.CancellationToken);

        Assert.Equal(now.UtcDateTime, result.GeneratedAt);
        Assert.Equal(7, store.RecentLimit);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await handler.Handle(
            new GetAdministrationDashboardQuery(21),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Search_NormalizesInputAndRejectsUnboundedPaging()
    {
        var store = new DiscoveryStore();
        var handler = new AdministrationDiscoveryHandler(store, TimeProvider.System);

        await handler.Handle(
            new SearchAdministrationApplicationsQuery("  identity  ", 4, 10),
            TestContext.Current.CancellationToken);

        Assert.Equal("identity", store.Search);
        Assert.Equal(4, store.Skip);
        Assert.Equal(10, store.Take);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await handler.Handle(
            new SearchAdministrationUsersQuery(null, 0, 51),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApplicationUsers_NormalizesInputAndRejectsUnboundedPaging()
    {
        var store = new DiscoveryStore();
        var handler = new AdministrationDiscoveryHandler(store, TimeProvider.System);

        await handler.Handle(
            new SearchAdministrationApplicationUsersQuery(10, "  identity  ", 4, 10),
            TestContext.Current.CancellationToken);

        Assert.Equal("identity", store.Search);
        Assert.Equal(4, store.Skip);
        Assert.Equal(10, store.Take);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await handler.Handle(
            new SearchAdministrationApplicationUsersQuery(0, null, 0, 10),
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await handler.Handle(
            new SearchAdministrationApplicationUsersQuery(10, null, 0, 51),
            TestContext.Current.CancellationToken));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class DiscoveryStore : IAdministrationDiscoveryStore
    {
        public int RecentLimit { get; private set; }
        public string? Search { get; private set; }
        public int Skip { get; private set; }
        public int Take { get; private set; }

        public Task<AdministrationDashboard> GetDashboard(
            int recentLimit,
            DateTime generatedAt,
            CancellationToken cancellationToken)
        {
            RecentLimit = recentLimit;
            return Task.FromResult(new AdministrationDashboard(
                0, 0, 0, 0, 0, 0, [], [], [], generatedAt));
        }

        public Task<PagedAdministrationApplications> SearchApplications(
            string? search,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            Capture(search, skip, take);
            return Task.FromResult(new PagedAdministrationApplications(skip, take, 0, []));
        }

        public Task<PagedAdministrationUsers> SearchUsers(
            string? search,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            Capture(search, skip, take);
            return Task.FromResult(new PagedAdministrationUsers(skip, take, 0, []));
        }

        public Task<AdministrationApplicationCatalog?> GetApplicationCatalog(
            long applicationId,
            CancellationToken cancellationToken) => Task.FromResult<AdministrationApplicationCatalog?>(
                new AdministrationApplicationCatalog(
                    new AdministrationApplicationSummary(
                        applicationId, "app", "Application", "urn:app", true, DateTime.UtcNow, null),
                    [],
                    [],
                    []));

        public Task<PagedAdministrationApplicationUsers?> SearchApplicationUsers(
            long applicationId,
            string? search,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            Capture(search, skip, take);
            return Task.FromResult<PagedAdministrationApplicationUsers?>(
                new PagedAdministrationApplicationUsers(skip, take, 0, []));
        }

        public Task<AdministrationUserAccessCatalog?> GetUserAccessCatalog(
            long userId,
            DateTime evaluatedAt,
            CancellationToken cancellationToken) => Task.FromResult<AdministrationUserAccessCatalog?>(
                new AdministrationUserAccessCatalog(
                    new AdministrationUserSummary(
                        userId, "EMP", "Employee", true, 1, null, null, evaluatedAt, null),
                    [],
                    [],
                    [],
                    evaluatedAt));

        public Task<AdministrationApplicationAccessCatalog?> GetApplicationAccessCatalog(
            long applicationId,
            CancellationToken cancellationToken) => Task.FromResult<AdministrationApplicationAccessCatalog?>(
                new AdministrationApplicationAccessCatalog(applicationId, [], [], []));

        public Task<AdministrationUserSecurityCatalog?> GetUserSecurityCatalog(
            long userId,
            DateTime evaluatedAt,
            CancellationToken cancellationToken) => Task.FromResult<AdministrationUserSecurityCatalog?>(
                new AdministrationUserSecurityCatalog(
                    new AdministrationUserSummary(
                        userId, "EMP", "Employee", true, 1, null, null, evaluatedAt, null),
                    [],
                    [],
                    []));

        public Task<AdministrationSecurityOperations> GetSecurityOperations(
            GetAdministrationSecurityOperationsQuery query,
            DateTime generatedAt,
            CancellationToken cancellationToken) => Task.FromResult(
                new AdministrationSecurityOperations(query.Skip, query.Take, 0, [], [], generatedAt));

        private void Capture(string? search, int skip, int take)
        {
            Search = search;
            Skip = skip;
            Take = take;
        }
    }
}
