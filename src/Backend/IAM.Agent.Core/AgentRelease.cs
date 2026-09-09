using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IAM.Agent.Core;

public sealed record ReleaseEnvelope(string Payload, string Signature);
public sealed record AgentRelease(string Product, int Schema, string Version, string Runtime, string PackageFile,
    long Size, string Sha256, DateTimeOffset ExpiresAt, string MinimumSupervisorVersion)
{
    public const string SupervisorVersion = "1.0.0";
    public const long MaximumPackageSize = 250_000_000;
    public static Version ParseVersion(string text)
    {
        if (text is null || !Regex.IsMatch(text, @"^\d{1,5}\.\d{1,5}\.\d{1,5}$")) throw new InvalidDataException("Invalid release version.");
        return System.Version.Parse(text);
    }
    public static AgentRelease Verify(ReadOnlySpan<byte> manifest, string publicKey, string currentVersion, DateTimeOffset now)
    {
        if (manifest.Length > 16_384) throw new InvalidDataException("Manifest exceeds size limit.");
        var envelope = JsonSerializer.Deserialize<ReleaseEnvelope>(manifest, AgentSettings.Json) ?? throw new InvalidDataException("Missing release envelope.");
        var payload = Convert.FromBase64String(envelope.Payload);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKey);
        if (rsa.KeySize < 3072 || !rsa.VerifyData(payload, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("Release signature verification failed.");
        var release = JsonSerializer.Deserialize<AgentRelease>(payload, AgentSettings.Json) ?? throw new InvalidDataException("Missing release data.");
        if (release.Product != "IAM.Agent" || release.Schema != 1 || release.Runtime != "win-x64"
            || release.PackageFile != $"IAM.Agent-{release.Version}-win-x64.zip"
            || release.Size is < 1 or > MaximumPackageSize || release.Sha256 is null || !Regex.IsMatch(release.Sha256, "^[A-Fa-f0-9]{64}$")
            || release.ExpiresAt <= now || ParseVersion(release.Version) < ParseVersion(currentVersion)
            || ParseVersion(release.MinimumSupervisorVersion) > ParseVersion(SupervisorVersion))
            throw new InvalidDataException("Expired, incompatible or downgraded release.");
        return release;
    }

    public async Task VerifyPackage(string path, CancellationToken ct = default)
    {
        using var file = File.OpenRead(path);
        if (file.Length != Size) throw new InvalidDataException("Package size mismatch.");
        var hash = await SHA256.HashDataAsync(file, ct);
        if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(Sha256))) throw new CryptographicException("Package hash mismatch.");
    }

    public static void ExtractPackage(string path, string destination)
    {
        if (Directory.Exists(destination)) throw new IOException("Version destination already exists.");
        AssertNoLinks(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        using var archive = ZipFile.OpenRead(path);
        // Self-contained single-file publish: allow exactly the agent executable, never paths or scripts.
        if (archive.Entries.Count != 1 || archive.Entries[0].FullName != "IAM.Agent.exe"
            || archive.Entries[0].Length is < 1 or > 500_000_000)
            throw new InvalidDataException("Package must contain only IAM.Agent.exe.");
        Directory.CreateDirectory(destination);
        using var input = archive.Entries[0].Open();
        using var output = new FileStream(Path.Combine(destination, "IAM.Agent.exe"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = input.Read(buffer)) != 0)
        {
            total += read;
            if (total > 500_000_000 || total > archive.Entries[0].Length) throw new InvalidDataException("Uncompressed package limit exceeded.");
            output.Write(buffer, 0, read);
        }
        if (total != archive.Entries[0].Length) throw new InvalidDataException("Incomplete package.");
        output.Flush(true);
    }

    public static void AssertNoLinks(string path)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(path)); directory is not null; directory = directory.Parent)
            if (directory.Exists && directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Agent paths may not contain links or junctions.");
    }
}
