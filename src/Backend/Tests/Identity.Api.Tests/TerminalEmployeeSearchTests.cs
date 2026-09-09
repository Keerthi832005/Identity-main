using System.Net;
using System.Net.Http.Json;
using Identity.Application.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Api.Tests;

public sealed class TerminalEmployeeSearchTests
{
    private const string Path = "/api/v1/auth/browser/terminal-employees/search";

    [Theory]
    [InlineData("EMP")]
    [InlineData("3275")]
    [InlineData("deswar")]
    public async Task Search_requires_browser_header_and_does_not_cache_or_return_admin_metadata(string search)
    {
        var directory = new Directory();
        await using var factory = new IdentityApiFactory();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<ITerminalEmployeeDirectory>(); s.AddSingleton<ITerminalEmployeeDirectory>(directory); }));
        using var client = host.CreateClient();
        var denied = await client.PostAsJsonAsync(Path, new { clientId = "pts-web", search = "EMP" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, directory.Calls);
        client.DefaultRequestHeaders.Add("X-Identity-Session", "browser");
        var response = await client.PostAsJsonAsync(Path, new { clientId = "pts-web", search = $" {search} " }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(search, directory.SearchTerm);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("employeeCode", json);
        Assert.Contains("displayName", json);
        Assert.DoesNotContain("userId", json);
        Assert.DoesNotContain("accessToken", json);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("EM")]
    [InlineData("EMP000000000000000000000000000000000000000000000000000000")]
    public async Task Empty_short_or_long_searches_do_not_query_directory(string? search)
    {
        var directory = new Directory();
        await using var factory = new IdentityApiFactory();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<ITerminalEmployeeDirectory>(); s.AddSingleton<ITerminalEmployeeDirectory>(directory); }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Identity-Session", "browser");
        var response = await client.PostAsJsonAsync(Path, new { clientId = "pts-web", search }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, directory.Calls);
    }

    [Fact]
    public async Task Search_limit_is_separate_from_password_verification()
    {
        var directory = new Directory();
        await using var factory = new IdentityApiFactory();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<ITerminalEmployeeDirectory>(); s.AddSingleton<ITerminalEmployeeDirectory>(directory); }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Identity-Session", "browser");
        for (var i = 0; i < 30; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Path, new { clientId = "pts-web", search = "EMP" }, TestContext.Current.CancellationToken)).StatusCode);
        var limited = await client.PostAsJsonAsync(Path, new { clientId = "pts-web", search = "EMP" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        var login = await client.PostAsJsonAsync("/api/v1/auth/browser/login", new { employeeCode = "unknown-user", password = "Fixture-only-password1!", clientId = "pts-web" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    private sealed class Directory : ITerminalEmployeeDirectory
    {
        public int Calls { get; private set; }
        public string? SearchTerm { get; private set; }
        public Task<IReadOnlyList<TerminalEmployeeSuggestion>> Search(string clientId, string search, CancellationToken cancellationToken)
        {
            Calls++;
            SearchTerm = search;
            return Task.FromResult<IReadOnlyList<TerminalEmployeeSuggestion>>([new("EMP001", "Test employee")]);
        }
    }
}
