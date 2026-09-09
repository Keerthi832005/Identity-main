using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Application.Administration;
using Identity.Contracts.Administration;

namespace Identity.Api.Tests;

public sealed class AdministrationApiTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetApplicationUsers_RequiresAuthenticatedAdministrator()
    {
        using var client = factory.CreateClient();
        using var anonymous = await client.GetAsync("/api/v1/admin/applications/10/users", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken(false));
        using var denied = await client.GetAsync("/api/v1/admin/applications/10/users", Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task GetApplicationUsers_MapsQueryAndReturnsRolesPerUser()
    {
        using var client = Admin();
        using var response = await client.GetAsync(
            "/api/v1/admin/applications/10/users?search=admin&skip=2&take=5",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.IsType<SearchAdministrationApplicationUsersQuery>(factory.Dispatcher.LastRequest);
        Assert.Equal(10, query.ApplicationId);
        Assert.Equal("admin", query.Search);
        Assert.Equal(2, query.Skip);
        Assert.Equal(5, query.Take);

        var page = await response.Content.ReadFromJsonAsync<PagedApplicationUsersResponse>(Token);
        var user = Assert.Single(page!.Items);
        Assert.Equal(42, user.UserId);
        Assert.Equal("ADMIN001", user.EmployeeCode);
        Assert.Equal(["identity-administrator"], user.Roles);
    }

    [Fact]
    public async Task SearchApplications_IncludesDescriptionAndCounts()
    {
        using var client = Admin();
        using var response = await client.GetAsync("/api/v1/admin/applications", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedApplicationsResponse>(Token);
        var application = Assert.Single(page!.Items);
        Assert.Equal(0, application.ClientCount);
        Assert.Equal(0, application.ModuleCount);
        Assert.Equal(0, application.RoleCount);
        Assert.Equal(0, application.UserCount);
    }

    [Fact]
    public async Task GetApplicationAccess_IncludesRoleMemberCount()
    {
        using var client = Admin();
        using var response = await client.GetAsync("/api/v1/admin/applications/10/access", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var access = await response.Content.ReadFromJsonAsync<ApplicationAccessCatalogResponse>(Token);
        var role = Assert.Single(access!.Roles);
        Assert.Equal(0, role.MemberCount);
    }

    private HttpClient Admin()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken());
        return client;
    }
}
