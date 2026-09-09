namespace Identity.Infrastructure.Security;

public sealed record SecurityProtectionOptions(
    string KeyId,
    byte[] EncryptionKey,
    byte[] ChallengeKey,
    byte[] IdentifierHashKey);
