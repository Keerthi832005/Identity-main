namespace Identity.Application.Authentication;

public sealed record SigningMetadata(string Issuer, JsonWebKey JsonWebKey);
