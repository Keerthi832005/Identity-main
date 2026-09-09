using System.Net;
using Identity.Api.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;

namespace Identity.Api.Tests;

public sealed class ReverseProxyConfigurationTests
{
    [Fact]
    public void Configure_UsesOnlyExplicitTrustedSources()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ReverseProxy:KnownProxies:0"] = "10.0.0.1",
                ["ReverseProxy:KnownNetworks:0"] = "192.0.2.0/24",
            }).Build();
        var options = new ForwardedHeadersOptions();

        ReverseProxyConfiguration.Configure(options, configuration);

        Assert.Equal(IPAddress.Parse("10.0.0.1"), Assert.Single(options.KnownProxies));
        Assert.Equal(System.Net.IPNetwork.Parse("192.0.2.0/24"),
            Assert.Single(options.KnownIPNetworks));
    }

    [Theory]
    [InlineData("ReverseProxy:ForwardLimit", "0")]
    [InlineData("ReverseProxy:KnownProxies:0", "invalid")]
    [InlineData("ReverseProxy:KnownNetworks:0", "invalid")]
    public void Configure_RejectsInvalidTrustSettings(string key, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [key] = value }).Build();

        Assert.Throws<InvalidOperationException>(() =>
            ReverseProxyConfiguration.Configure(new ForwardedHeadersOptions(), configuration));
    }
}
