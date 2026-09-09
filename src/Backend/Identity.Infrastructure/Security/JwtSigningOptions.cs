namespace Identity.Infrastructure.Security;

public sealed record JwtSigningOptions(string Issuer, string KeyId, string PrivateKeyPem);
