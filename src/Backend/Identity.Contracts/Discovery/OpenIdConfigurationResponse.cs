using System.Text.Json.Serialization;

namespace Identity.Contracts.Discovery;

public sealed record OpenIdConfigurationResponse(
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("jwks_uri")] string JwksUri,
    [property: JsonPropertyName("id_token_signing_alg_values_supported")]
    IReadOnlyList<string> SigningAlgorithmsSupported);
