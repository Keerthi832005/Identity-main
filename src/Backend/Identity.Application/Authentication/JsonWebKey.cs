namespace Identity.Application.Authentication;

public sealed record JsonWebKey(
    string KeyType,
    string Use,
    string KeyId,
    string Algorithm,
    string Modulus,
    string Exponent);
