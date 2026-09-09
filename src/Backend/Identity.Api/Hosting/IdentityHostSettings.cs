using System.Security.Cryptography;

namespace Identity.Api.Hosting;

internal sealed record IdentityHostSettings(
    string ConnectionString,
    string Issuer,
    string KeyId,
    string PrivateKeyPem,
    string AdministrationAudience,
    string SecurityKeyId,
    byte[] EncryptionKey,
    byte[] ChallengeKey,
    byte[] IdentifierHashKey,
    int AuthenticationPermitLimit,
    int SessionRefreshPermitLimit,
    int SessionLogoutPermitLimit,
    string BrowserSessionCookieName,
    string BrowserSessionCookiePath,
    bool RequireSecureBrowserSessionCookie)
{
    public static IdentityHostSettings Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = Required(
            configuration.GetConnectionString("Identity"),
            "ConnectionStrings:Identity");
        var issuer = Required(configuration["Identity:Jwt:Issuer"], "Identity:Jwt:Issuer");
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri)
            || (issuerUri.Scheme != Uri.UriSchemeHttps && !issuerUri.IsLoopback))
        {
            throw Invalid("Identity:Jwt:Issuer");
        }

        var privateKeyPem = Required(
            configuration["Identity:Jwt:PrivateKeyPem"],
            "Identity:Jwt:PrivateKeyPem");
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            if (rsa.KeySize < 2048)
            {
                throw Invalid("Identity:Jwt:PrivateKeyPem");
            }
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            throw Invalid("Identity:Jwt:PrivateKeyPem");
        }

        var permitLimit = ReadRateLimit(configuration, "AuthenticationPermitLimit", 10);
        var refreshPermitLimit = ReadRateLimit(configuration, "SessionRefreshPermitLimit", 30);
        var logoutPermitLimit = ReadRateLimit(configuration, "SessionLogoutPermitLimit", 30);

        var cookieName = Required(
            configuration["Identity:BrowserSession:CookieName"],
            "Identity:BrowserSession:CookieName");
        if (cookieName.Any(value => char.IsWhiteSpace(value) || value is ';' or '=' or ','))
        {
            throw Invalid("Identity:BrowserSession:CookieName");
        }

        var cookiePath = Required(
            configuration["Identity:BrowserSession:CookiePath"],
            "Identity:BrowserSession:CookiePath");
        if (!cookiePath.StartsWith('/') || cookiePath.StartsWith("//", StringComparison.Ordinal))
        {
            throw Invalid("Identity:BrowserSession:CookiePath");
        }

        var requireSecureCookie = configuration.GetValue(
            "Identity:BrowserSession:RequireSecureCookie",
            true);

        return new IdentityHostSettings(
            connectionString,
            issuer.TrimEnd('/'),
            Required(configuration["Identity:Jwt:KeyId"], "Identity:Jwt:KeyId"),
            privateKeyPem,
            Required(
                configuration["Identity:Jwt:AdministrationAudience"],
                "Identity:Jwt:AdministrationAudience"),
            Required(configuration["Identity:Security:KeyId"], "Identity:Security:KeyId"),
            DecodeKey(configuration, "Identity:Security:EncryptionKey"),
            DecodeKey(configuration, "Identity:Security:ChallengeKey"),
            DecodeKey(configuration, "Identity:Security:IdentifierHashKey"),
            permitLimit,
            refreshPermitLimit,
            logoutPermitLimit,
            cookieName,
            cookiePath,
            requireSecureCookie);
    }

    private static int ReadRateLimit(IConfiguration configuration, string name, int defaultValue)
    {
        var key = $"Identity:RateLimit:{name}";
        var limit = configuration.GetValue(key, defaultValue);
        return limit is >= 1 and <= 1000 ? limit : throw Invalid(key);
    }

    private static byte[] DecodeKey(IConfiguration configuration, string name)
    {
        try
        {
            var value = Convert.FromBase64String(Required(configuration[name], name));
            return value.Length == 32 ? value : throw Invalid(name);
        }
        catch (FormatException)
        {
            throw Invalid(name);
        }
    }

    private static string Required(string? value, string name) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw Invalid(name);

    private static InvalidOperationException Invalid(string name) => new(
        $"Required Identity host setting '{name}' is missing or invalid.");
}
