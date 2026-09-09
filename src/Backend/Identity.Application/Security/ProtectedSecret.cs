namespace Identity.Application.Security;

public sealed record ProtectedSecret(byte[] Ciphertext, string KeyId);
