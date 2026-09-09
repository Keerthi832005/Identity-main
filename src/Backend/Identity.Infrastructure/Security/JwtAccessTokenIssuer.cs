using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Identity.Application.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Security;

internal sealed class JwtAccessTokenIssuer : IAccessTokenIssuer, ITerminalAccessTokenValidator, IDisposable
{
    private const int MaximumCapabilityClaims = 256;
    private readonly string issuer;
    private readonly RsaSecurityKey securityKey;
    private readonly SigningCredentials signingCredentials;
    private readonly JwtSecurityTokenHandler tokenHandler = new();

    public JwtAccessTokenIssuer(JwtSigningOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Uri.TryCreate(options.Issuer, UriKind.Absolute, out var issuerUri)
            || (issuerUri.Scheme != Uri.UriSchemeHttps && !issuerUri.IsLoopback))
        {
            throw new ArgumentException(
                "JWT issuer must be an absolute HTTPS URI, except for loopback development.",
                nameof(options));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(options.KeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PrivateKeyPem);
        issuer = options.Issuer.TrimEnd('/');
        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(options.PrivateKeyPem);
            if (rsa.KeySize < 2048)
            {
                throw new ArgumentException("JWT RSA keys must contain at least 2048 bits.", nameof(options));
            }

            securityKey = new RsaSecurityKey(rsa) { KeyId = options.KeyId };
            signingCredentials = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.RsaSha256);
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    public IssuedAccessToken Issue(AccessTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpiresAt <= request.IssuedAt
            || request.ExpiresAt - request.IssuedAt > TimeSpan.FromMinutes(60))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Access-token lifetime is invalid.");
        }

        if (request.CapabilityCodes.Count > MaximumCapabilityClaims)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Access tokens support at most {MaximumCapabilityClaims} capability claims.");
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(
                JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(request.IssuedAt).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
            new("employee_code", request.EmployeeCode),
            new("display_name", request.DisplayName),
            new("application_id", request.ApplicationId.ToString(CultureInfo.InvariantCulture)),
            new("client_id", request.ClientId),
            new("security_version", request.SecurityVersion.ToString(CultureInfo.InvariantCulture)),
            new("authorization_version", request.AuthorizationVersion.ToString(CultureInfo.InvariantCulture)),
        };
        claims.AddRange(request.CapabilityCodes.Select(capability => new Claim("capability", capability)));
        var token = new JwtSecurityToken(
            issuer,
            request.Audience,
            claims,
            request.IssuedAt,
            request.ExpiresAt,
            signingCredentials);
        return new IssuedAccessToken(tokenHandler.WriteToken(token), request.ExpiresAt);
    }

    public TerminalTokenIdentity? Validate(string accessToken, string audience, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 32768) return null;
        try
        {
            var validator = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = validator.ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = securityKey,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.Zero,
                LifetimeValidator = (notBefore, expires, _, _) =>
                    expires.HasValue && expires.Value > now && (!notBefore.HasValue || notBefore.Value <= now),
            }, out _);
            string? Single(string name)
            {
                var claims = principal.FindAll(name).ToArray();
                return claims.Length == 1 ? claims[0].Value : null;
            }
            if (!long.TryParse(Single("sub"), NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0
                || !long.TryParse(Single("application_id"), NumberStyles.None, CultureInfo.InvariantCulture, out var appId) || appId <= 0
                || !int.TryParse(Single("security_version"), NumberStyles.None, CultureInfo.InvariantCulture, out var security) || security < 1
                || !int.TryParse(Single("authorization_version"), NumberStyles.None, CultureInfo.InvariantCulture, out var authorization) || authorization < 1
                || Single("client_id") is not { Length: > 0 } clientId)
                return null;
            return new TerminalTokenIdentity(userId, appId, clientId, security, authorization);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    public SigningMetadata GetSigningMetadata()
    {
        var parameters = securityKey.Rsa.ExportParameters(false);
        return new SigningMetadata(
            issuer,
            new Identity.Application.Authentication.JsonWebKey(
                "RSA",
                "sig",
                securityKey.KeyId,
                "RS256",
                Base64UrlEncoder.Encode(parameters.Modulus),
                Base64UrlEncoder.Encode(parameters.Exponent)));
    }

    public void Dispose() => securityKey.Rsa.Dispose();
}
