namespace Identity.Application.Security;

public interface ITotpService
{
    byte[] GenerateSecret();
    string EncodeSecret(byte[] secret);
    bool Verify(byte[] secret, string code, DateTime timestamp);
    bool TryVerify(byte[] secret, string code, DateTime timestamp, out long matchedTimeStep);
}
