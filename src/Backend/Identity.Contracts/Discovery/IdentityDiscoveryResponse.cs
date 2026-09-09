namespace Identity.Contracts.Discovery;

public sealed record IdentityDiscoveryResponse(
    string Issuer,
    string JwksUri,
    string LoginEndpoint,
    string MfaEndpoint,
    string RefreshEndpoint,
    IReadOnlyList<string> SigningAlgorithmsSupported);
