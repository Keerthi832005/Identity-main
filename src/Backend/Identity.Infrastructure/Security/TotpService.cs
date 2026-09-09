using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Identity.Application.Security;

namespace Identity.Infrastructure.Security;

internal sealed class TotpService : ITotpService
{
    private const int SecretLength = 20;
    private const int PeriodSeconds = 30;
    private const int Digits = 6;
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public byte[] GenerateSecret() => RandomNumberGenerator.GetBytes(SecretLength);

    public string EncodeSecret(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length == 0)
        {
            throw new ArgumentException("TOTP secret is required.", nameof(secret));
        }

        var output = new StringBuilder((secret.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var value in secret)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                output.Append(Alphabet[(buffer >> bits) & 31]);
            }
        }

        if (bits > 0)
        {
            output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return output.ToString();
    }

    public bool Verify(byte[] secret, string code, DateTime timestamp) =>
        TryVerify(secret, code, timestamp, out _);

    public bool TryVerify(
        byte[] secret, string code, DateTime timestamp, out long matchedTimeStep)
    {
        ArgumentNullException.ThrowIfNull(secret);
        matchedTimeStep = -1;
        if (string.IsNullOrEmpty(code) || code.Length != Digits || !code.All(char.IsAsciiDigit))
        {
            return false;
        }

        var counter = new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc))
            .ToUnixTimeSeconds() / PeriodSeconds;
        for (var offset = -1; offset <= 1; offset++)
        {
            var expected = Compute(secret, counter + offset);
            if (CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(code),
                Encoding.ASCII.GetBytes(expected)))
            {
                matchedTimeStep = counter + offset;
                return true;
            }
        }

        return false;
    }

    private static string Compute(byte[] secret, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);
        var hash = HMACSHA1.HashData(secret, counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return (binary % 1000000).ToString("D6", CultureInfo.InvariantCulture);
    }
}
