using System.Text.Json;
using System.Text.RegularExpressions;

namespace IAM.Agent.Core;

public sealed record AgentSettings(
    string SiteOrigin,
    Guid InstallationId,
    long TerminalId,
    string UpdateFeed,
    string UpdatePublicKey,
    int UpdateIntervalMinutes = 60,
    string IdentityBaseUrl = "",
    bool AllowLoopbackDevelopmentOrigins = false)
{
    public const int Port = 43127;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public bool AllowsOrigin(string origin)
    {
        if (origin == SiteOrigin) return true;
        // Opt in per installation. Match exact loopback hostnames, never URL prefixes,
        // alternative IP spellings, user-info, paths, or lookalike domains.
        return AllowLoopbackDevelopmentOrigins
            && Regex.IsMatch(origin, @"\Ahttp://(?:localhost|127\.0\.0\.1)(?::[0-9]{1,5})?\z", RegexOptions.CultureInvariant)
            && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && uri.Port is > 0 and <= 65535;
    }

    public static AgentSettings Load(string path)
    {
        var settings = JsonSerializer.Deserialize<AgentSettings>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException("Agent configuration is missing.");
        settings.Validate();
        return settings;
    }

    public void Validate()
    {
        if (!Uri.TryCreate(SiteOrigin, UriKind.Absolute, out var site) || site.Scheme != "https"
            || SiteOrigin != site.GetLeftPart(UriPartial.Authority) || site.UserInfo.Length != 0)
            throw new InvalidDataException("SiteOrigin must be an exact HTTPS origin without a trailing slash.");
        if (InstallationId == Guid.Empty || TerminalId <= 0 || TerminalId > 9007199254740991)
            throw new InvalidDataException("An installed identity and registered IAM terminal are required.");
        if (!Uri.TryCreate(IdentityBaseUrl, UriKind.Absolute, out var identity) || identity.Scheme != "https"
            || identity.UserInfo.Length != 0 || identity.Query.Length != 0 || identity.Fragment.Length != 0 || !identity.AbsolutePath.EndsWith('/'))
            throw new InvalidDataException("IdentityBaseUrl must be an HTTPS IAM API base directory.");
        if (!Uri.TryCreate(UpdateFeed, UriKind.Absolute, out var feed) || feed.Scheme != "https"
            || feed.GetLeftPart(UriPartial.Authority) != identity.GetLeftPart(UriPartial.Authority) || feed.UserInfo.Length != 0
            || feed.Query.Length != 0 || feed.Fragment.Length != 0 || !feed.AbsolutePath.EndsWith('/'))
            throw new InvalidDataException("UpdateFeed must be an HTTPS directory on the configured IAM server.");
        if (UpdateIntervalMinutes is < 5 or > 1440 || string.IsNullOrWhiteSpace(UpdatePublicKey))
            throw new InvalidDataException("A release public key and update interval from 5 to 1440 minutes are required.");
        using var key = System.Security.Cryptography.RSA.Create();
        key.ImportFromPem(UpdatePublicKey);
        if (key.KeySize < 3072) throw new InvalidDataException("Release signing key must be at least RSA-3072.");
    }
}
