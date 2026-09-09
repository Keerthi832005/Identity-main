using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Identity.Api.Hosting;

public static class ReverseProxyConfiguration
{
    public const string SectionName = "ReverseProxy";

    public static IServiceCollection AddConfiguredForwardedHeaders(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options => Configure(options, configuration));
        return services;
    }

    public static void Configure(
        ForwardedHeadersOptions options, IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var forwardLimit = section.GetValue<int?>("ForwardLimit") ?? 1;
        if (forwardLimit <= 0)
        {
            throw new InvalidOperationException("ReverseProxy:ForwardLimit must be positive.");
        }

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = forwardLimit;
        options.RequireHeaderSymmetry = true;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var value in section.GetSection("KnownProxies").Get<string[]>() ?? [])
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                throw new InvalidOperationException(
                    $"ReverseProxy:KnownProxies contains invalid IP address '{value}'.");
            }

            options.KnownProxies.Add(address);
        }

        foreach (var value in section.GetSection("KnownNetworks").Get<string[]>() ?? [])
        {
            if (!System.Net.IPNetwork.TryParse(value, out var network))
            {
                throw new InvalidOperationException(
                    $"ReverseProxy:KnownNetworks contains invalid CIDR network '{value}'.");
            }

            options.KnownIPNetworks.Add(network);
        }
    }
}
