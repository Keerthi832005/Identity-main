namespace Identity.Contracts.Discovery;

public sealed record JsonWebKeyResponse(
    string Kty,
    string Use,
    string Kid,
    string Alg,
    string N,
    string E);
