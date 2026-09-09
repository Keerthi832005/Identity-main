namespace Identity.Application.Authentication;

public interface ISecretHasher
{
    byte[] Hash(string secret);
    bool Verify(string secret, byte[] expectedHash);
}
