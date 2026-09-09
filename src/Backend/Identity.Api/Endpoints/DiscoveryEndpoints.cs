using Identity.Application.Authentication;
using Identity.Application.Messaging;
using Identity.Contracts.Discovery;

namespace Identity.Api.Endpoints;

internal static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapIdentityDiscoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/.well-known/openid-configuration", async (
            HttpContext context,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var metadata = await dispatcher.Send(new GetSigningMetadataQuery(), cancellationToken);
            return new OpenIdConfigurationResponse(
                metadata.Issuer,
                BuildEndpoint(context, "/.well-known/jwks.json"),
                [metadata.JsonWebKey.Algorithm]);
        }).AllowAnonymous();
        endpoints.MapGet("/.well-known/identity-configuration", async (
            HttpContext context,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var metadata = await dispatcher.Send(new GetSigningMetadataQuery(), cancellationToken);
            return new IdentityDiscoveryResponse(
                metadata.Issuer,
                BuildEndpoint(context, "/.well-known/jwks.json"),
                BuildEndpoint(context, "/api/v1/auth/login"),
                BuildEndpoint(context, "/api/v1/auth/mfa/complete"),
                BuildEndpoint(context, "/api/v1/auth/refresh"),
                [metadata.JsonWebKey.Algorithm]);
        }).AllowAnonymous();
        endpoints.MapGet("/.well-known/jwks.json", async (
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var metadata = await dispatcher.Send(new GetSigningMetadataQuery(), cancellationToken);
            var key = metadata.JsonWebKey;
            return new JsonWebKeySetResponse([
                new JsonWebKeyResponse(
                    key.KeyType,
                    key.Use,
                    key.KeyId,
                    key.Algorithm,
                    key.Modulus,
                    key.Exponent),
            ]);
        }).AllowAnonymous();
        return endpoints;
    }

    private static string BuildEndpoint(HttpContext context, string path) =>
        $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{path}";
}
