namespace Identity.AdminCli;

internal sealed record BootstrapSecrets(
    string ConnectionString,
    string Issuer,
    string SigningKeyId,
    string PrivateKeyPem,
    string SecurityKeyId,
    byte[] EncryptionKey,
    byte[] ChallengeKey,
    byte[] IdentifierHashKey,
    string Password,
    string ClientSecret)
{
    public static BootstrapSecrets Load() => new(
        Required("IDENTITY_DATABASE_CONNECTION"),
        Required("IDENTITY_JWT_ISSUER"),
        Required("IDENTITY_JWT_KEY_ID"),
        Required("IDENTITY_JWT_PRIVATE_KEY_PEM"),
        Required("IDENTITY_SECURITY_KEY_ID"),
        DecodeKey("IDENTITY_ENCRYPTION_KEY"),
        DecodeKey("IDENTITY_CHALLENGE_KEY"),
        DecodeKey("IDENTITY_IDENTIFIER_HASH_KEY"),
        Required("IDENTITY_BOOTSTRAP_PASSWORD"),
        Required("IDENTITY_BOOTSTRAP_CLIENT_SECRET"));

    private static byte[] DecodeKey(string name)
    {
        try
        {
            var value = Convert.FromBase64String(Required(name));
            return value.Length == 32
                ? value
                : throw new InvalidOperationException($"Environment variable '{name}' is invalid.");
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"Environment variable '{name}' is invalid.");
        }
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Environment variable '{name}' is required.");
}
