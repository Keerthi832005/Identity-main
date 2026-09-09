namespace Identity.Application.Security;

public interface ISecretProtector
{
    ProtectedSecret Protect(byte[] plaintext);
    byte[] Unprotect(byte[] ciphertext, string keyId);
}
