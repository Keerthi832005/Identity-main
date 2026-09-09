using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Application.Authentication;
using Identity.Application.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Api.Tests;

public sealed class CurrentTerminalVerificationApiTests
{
    [Theory]
    [InlineData(null, HttpStatusCode.OK)]
    [InlineData(TerminalVerificationFailureCode.TerminalNotTrusted, HttpStatusCode.Forbidden)]
    [InlineData(TerminalVerificationFailureCode.InvalidCredentials, HttpStatusCode.Unauthorized)]
    [InlineData(TerminalVerificationFailureCode.PinChangeRequired, HttpStatusCode.Conflict)]
    [InlineData(TerminalVerificationFailureCode.InvalidNewPin, HttpStatusCode.Conflict)]
    public async Task MapsServiceVerificationAndNeverReturnsBearer(TerminalVerificationFailureCode? failure, HttpStatusCode expected)
    {
        var dispatcher = new Dispatcher(failure);
        await using var factory = new IdentityApiFactory();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IRequestDispatcher>();
            s.AddSingleton<IRequestDispatcher>(dispatcher);
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "fixture-bearer");
        using var response = await client.PostAsJsonAsync("/api/v1/auth/terminal/current",
            new { terminalId = 77, clientId = "pts-service", clientSecret = "fixture-secret", newPin = "2468" }, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("fixture-bearer", dispatcher.Last!.AccessToken);
        Assert.Equal("fixture-secret", dispatcher.Last.ClientSecret);
        Assert.Equal("2468", dispatcher.Last.NewPin);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("fixture-bearer", body);
        Assert.DoesNotContain("fixture-secret", body);
        Assert.DoesNotContain("2468", body);
    }

    [Fact]
    public async Task MissingBearerDoesNotDispatchAUserVerification()
    {
        var dispatcher = new Dispatcher(null);
        await using var factory = new IdentityApiFactory();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IRequestDispatcher>();
            s.AddSingleton<IRequestDispatcher>(dispatcher);
        }));
        using var client = host.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/terminal/current",
            new { terminalId = 77, clientId = "pts-service", clientSecret = "fixture-secret" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(dispatcher.Last);
    }

    private sealed class Dispatcher(TerminalVerificationFailureCode? failure) : IRequestDispatcher
    {
        public CurrentTerminalVerificationCommand? Last;
        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Last = Assert.IsType<CurrentTerminalVerificationCommand>(request);
            var response = failure.HasValue ? TerminalVerificationResult.Rejected(failure.Value)
                : TerminalVerificationResult.Verified(42, "E42", "Fixture", ["pts.shopfloor.operate", "pts.production.read"]);
            return ValueTask.FromResult((TResponse)(object)response);
        }
    }
}
