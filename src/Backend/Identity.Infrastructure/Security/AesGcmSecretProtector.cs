using System.Security.Cryptography;
using Identity.Application.Security;

namespace Identity.Infrastructure.Security;

internal sealed class AesGcmSecretProtector : ISecretProtector
{
    private const byte EnvelopeVersion = 1;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private readonly string keyId;
    private readonly byte[] key;

    public AesGcmSecretProtector(SecurityProtectionOptions options)
    {
        ValidateKey(options.EncryptionKey, nameof(options.EncryptionKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.KeyId);
        keyId = options.KeyId;
        key = [.. options.EncryptionKey];
    }

    public ProtectedSecret Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        if (plaintext.Length == 0)
        {
            throw new ArgumentException("Plaintext is required.", nameof(plaintext));
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];
        using var aes = new AesGcm(key, TagLength);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        var envelope = new byte[1 + NonceLength + TagLength + ciphertext.Length];
        envelope[0] = EnvelopeVersion;
        nonce.CopyTo(envelope, 1);
        tag.CopyTo(envelope, 1 + NonceLength);
        ciphertext.CopyTo(envelope, 1 + NonceLength + TagLength);
        return new ProtectedSecret(envelope, keyId);
    }

    public byte[] Unprotect(byte[] ciphertext, string protectedKeyId)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (!string.Equals(keyId, protectedKeyId, StringComparison.Ordinal)
            || ciphertext.Length <= 1 + NonceLength + TagLength
            || ciphertext[0] != EnvelopeVersion)
        {
            throw new CryptographicException("Protected secret envelope is invalid.");
        }

        var nonce = ciphertext.AsSpan(1, NonceLength);
        var tag = ciphertext.AsSpan(1 + NonceLength, TagLength);
        var encrypted = ciphertext.AsSpan(1 + NonceLength + TagLength);
        var plaintext = new byte[encrypted.Length];
        using var aes = new AesGcm(key, TagLength);
        aes.Decrypt(nonce, encrypted, tag, plaintext);
        return plaintext;
    }

    private static void ValidateKey(byte[] value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (value.Length != 32)
        {
            throw new ArgumentException("Security keys must contain 32 bytes.", name);
        }
    }
}
