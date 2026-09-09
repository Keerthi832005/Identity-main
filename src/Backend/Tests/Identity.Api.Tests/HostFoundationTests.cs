using System.Net;
using System.Net.Http.Json;
using Identity.Api.Health;

namespace Identity.Api.Tests;

public sealed class HostFoundationTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task LivenessEndpoint_ReturnsNamedHealthyResponse()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<HealthStatusResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new HealthStatusResponse("Healthy"), body);
    }

    [Fact]
    public async Task ReadinessAndOpenApiEndpoints_ArePublicAndAvailable()
    {
        using var client = factory.CreateClient();

        using var readiness = await client.GetAsync(
            "/health/ready",
            TestContext.Current.CancellationToken);
        using var openApi = await client.GetAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        Assert.Equal("application/json", openApi.Content.Headers.ContentType?.MediaType);
    }
}
