namespace Identity.Application.Security;

public interface IChallengeHasher
{
    string KeyId { get; }
    byte[] Hash(Guid challengeId);
    bool Verify(Guid challengeId, byte[] expectedHash, string keyId);
}
