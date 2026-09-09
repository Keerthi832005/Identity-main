using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Identity.Application.Administration;
using Identity.Contracts.Administration;
using Identity.Contracts.Errors;
using Identity.Domain.Enums;

namespace Identity.Api.Tests;

public sealed class OrganizationApiTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AllRoutes_RequireAuthenticatedAdministrator()
    {
        foreach (var (method, path) in new[] { (HttpMethod.Get,"/api/v1/admin/organization-units"), (HttpMethod.Get,"/api/v1/admin/organizations/10/units/100"),
   (HttpMethod.Post,"/api/v1/admin/organizations"), (HttpMethod.Post,"/api/v1/admin/organizations/10/units"),
   (HttpMethod.Put,"/api/v1/admin/organizations/10/units/100"), (HttpMethod.Put,"/api/v1/admin/organizations/10/units/100/active") })
        {
            using var client = factory.CreateClient();
            using var anonymous = new HttpRequestMessage(method, path) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            using var response = await client.SendAsync(anonymous, Token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken(false));
            using var denied = new HttpRequestMessage(method, path) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            using var denial = await client.SendAsync(denied, Token);
            Assert.Equal(HttpStatusCode.Forbidden, denial.StatusCode);
        }
    }

    [Fact]
    public async Task Creation_UsesAtomicTypedCommandsAndServerActor()
    {
        using var client = Admin();
        var address = new OrganizationAddressData(City: "Chennai", Latitude: 13);
        using var root = await client.PostAsJsonAsync("/api/v1/admin/organizations", new CreateOrganizationRequest("ORG", "Organization", "Description", address), Token);
        Assert.Equal(HttpStatusCode.Created, root.StatusCode);
        Assert.Equal("/api/v1/admin/organizations/10/units/100", root.Headers.Location?.ToString());
        var rootCommand = Assert.IsType<CreateOrganizationCommand>(factory.Dispatcher.LastRequest);
        Assert.Equal(42, rootCommand.Context.ActorUserId);
        Assert.Equal("Chennai", rootCommand.Address?.City);
        Assert.Equal("Description", rootCommand.Description);
        foreach (var type in Enum.GetValues<OrganizationUnitType>().Where(type => type != OrganizationUnitType.Organization))
        {
            using var child = await client.PostAsJsonAsync("/api/v1/admin/organizations/10/units", new CreateOrganizationUnitRequest(type.ToString(), 100, "CODE", "Name", null, address), Token);
            Assert.Equal(HttpStatusCode.Created, child.StatusCode);
            var command = Assert.IsType<CreateOrganizationUnitCommand>(factory.Dispatcher.LastRequest);
            Assert.Equal(type, command.UnitType);
            Assert.Equal(10, command.OrganizationId);
            Assert.Equal(100, command.ParentOrganizationUnitId);
            Assert.Equal(42, command.Context.ActorUserId);
        }
    }

    [Fact]
    public async Task Queries_AndUpdatesMapTypedFieldsAndConcurrency()
    {
        using var client = Admin();
        using var list = await client.GetAsync("/api/v1/admin/organization-units?organizationId=10&unitType=Country&parentOrganizationUnitId=100&isActive=false&search=West&skip=2&take=5", Token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var query = Assert.IsType<SearchOrganizationUnitsQuery>(factory.Dispatcher.LastRequest);
        Assert.Equal(10, query.OrganizationId);
        Assert.Equal(OrganizationUnitType.Country, query.UnitType);
        Assert.Equal(100, query.ParentOrganizationUnitId);
        Assert.False(query.IsActive);
        Assert.Equal("West", query.Search);
        Assert.Equal(2, query.Skip);
        Assert.Equal(5, query.Take);
        var page = await list.Content.ReadFromJsonAsync<PagedOrganizationUnitsResponse>(Token);
        Assert.Equal("Organization", Assert.Single(page!.Items).UnitType);
        using var detail = await client.GetAsync("/api/v1/admin/organizations/10/units/100", Token);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var version = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        using var update = await client.PutAsJsonAsync("/api/v1/admin/organizations/10/units/100", new UpdateOrganizationUnitRequest(version, "CODE", "Name", null, new()), Token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var command = Assert.IsType<UpdateOrganizationUnitCommand>(factory.Dispatcher.LastRequest);
        Assert.Equal(version, command.RowVersion);
        Assert.Equal(10, command.OrganizationId);
        Assert.Equal(100, command.OrganizationUnitId);
        using var state = await client.PutAsJsonAsync("/api/v1/admin/organizations/10/units/100/active", new SetOrganizationUnitActiveRequest(false, version), Token);
        Assert.Equal(HttpStatusCode.OK, state.StatusCode);
        Assert.False(Assert.IsType<SetOrganizationUnitActiveCommand>(factory.Dispatcher.LastRequest).IsActive);
    }

    [Fact]
    public async Task Validation_RejectsNestedAddressBoundsMissingVersionAndImmutableFields()
    {
        using var client = Admin();
        foreach (var request in new[] {
   new CreateOrganizationRequest(" ","Name",null,null),
   new CreateOrganizationRequest("CODE",new string('x',201),null,null),
   new CreateOrganizationRequest("CODE","Name",null,new(Latitude:91)),
   new CreateOrganizationRequest("CODE","Name",null,new(AddressLine3:new string('x',251))) })
        {
            using var response = await client.PostAsJsonAsync("/api/v1/admin/organizations", request, Token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var badType = await client.PostAsJsonAsync("/api/v1/admin/organizations/10/units", new CreateOrganizationUnitRequest("Organization", 100, "X", "Name", null, null), Token);
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        using var badParent = await client.PostAsJsonAsync("/api/v1/admin/organizations/10/units", new CreateOrganizationUnitRequest("Country", 0, "X", "Name", null, null), Token);
        Assert.Equal(HttpStatusCode.BadRequest, badParent.StatusCode);
        using var version = await client.PutAsJsonAsync("/api/v1/admin/organizations/10/units/100", new UpdateOrganizationUnitRequest([], "X", "Name", null, new()), Token);
        Assert.Equal(HttpStatusCode.BadRequest, version.StatusCode);
        using var missingState = await client.PutAsJsonAsync("/api/v1/admin/organizations/10/units/100/active", new SetOrganizationUnitActiveRequest(null, new byte[8]), Token);
        Assert.Equal(HttpStatusCode.BadRequest, missingState.StatusCode);
        using var immutable = await client.PutAsync("/api/v1/admin/organizations/10/units/100", new StringContent("""
   {"unitCode":"X","unitName":"Name","address":{},"rowVersion":"AAAAAAAAAAA=","parentOrganizationUnitId":123}
   """, Encoding.UTF8, "application/json"), Token);
        Assert.Equal(HttpStatusCode.BadRequest, immutable.StatusCode);
        using var numericType = await client.GetAsync("/api/v1/admin/organization-units?unitType=1", Token);
        Assert.Equal(HttpStatusCode.BadRequest, numericType.StatusCode);
    }

    [Fact]
    public async Task Failures_ReturnDistinctSafeConflictAndMissingCodes()
    {
        using var client = Admin();
        using var stale = await client.PutAsJsonAsync("/api/v1/admin/organizations/10/units/100", new UpdateOrganizationUnitRequest(new byte[8], "CODE", "STALE", null, new()), Token);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("organization_concurrency_conflict", (await stale.Content.ReadFromJsonAsync<ApiErrorResponse>(Token))?.Code);
        using var duplicate = await client.PostAsJsonAsync("/api/v1/admin/organizations", new CreateOrganizationRequest("DUPLICATE", "Name", null, null), Token);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("organization_code_conflict", (await duplicate.Content.ReadFromJsonAsync<ApiErrorResponse>(Token))?.Code);
        using var missing = await client.GetAsync("/api/v1/admin/organizations/999/units/100", Token);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var deletion = await client.DeleteAsync("/api/v1/admin/organizations/10/units/100", Token);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, deletion.StatusCode);
    }

    private HttpClient Admin()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken());
        return client;
    }
}
