using System.Security.Cryptography;
using Identity.Application.Authentication;

namespace Identity.Infrastructure.Security;

internal sealed class RandomTokenGenerator : IRandomTokenGenerator
{
    private const int TokenLength = 32;

    public string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenLength))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
