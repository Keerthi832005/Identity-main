using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using IAM.Agent.Core;

namespace IAM.Agent.Tests;

public sealed class ReleaseTests
{
    [Fact]
    public void Release_requires_signature_correct_product_version_freshness_and_supervisor_compatibility()
    {
        using var key = RSA.Create(3072);
        var now = DateTimeOffset.UtcNow;
        var release = new AgentRelease("IAM.Agent", 1, "1.1.0", "win-x64", "IAM.Agent-1.1.0-win-x64.zip", 12, new string('a', 64), now.AddDays(30), "1.0.0");
        byte[] Sign(AgentRelease value)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, AgentSettings.Json);
            return JsonSerializer.SerializeToUtf8Bytes(new ReleaseEnvelope(Convert.ToBase64String(bytes),
                Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))), AgentSettings.Json);
        }
        var pem = key.ExportSubjectPublicKeyInfoPem();
        Assert.Equal("1.1.0", AgentRelease.Verify(Sign(release), pem, "1.0.0", now).Version);
        Assert.Throws<InvalidDataException>(() => AgentRelease.Verify(Sign(release), pem, "1.2.0", now));
        Assert.Throws<InvalidDataException>(() => AgentRelease.Verify(Sign(release with { ExpiresAt = now }), pem, "1.0.0", now));
        Assert.Throws<InvalidDataException>(() => AgentRelease.Verify(Sign(release with { PackageFile = "../evil.zip" }), pem, "1.0.0", now));
        Assert.Throws<InvalidDataException>(() => AgentRelease.Verify(Sign(release with { Product = "AnotherProduct" }), pem, "1.0.0", now));
        Assert.Throws<InvalidDataException>(() => AgentRelease.Verify(Sign(release with { MinimumSupervisorVersion = "2.0.0" }), pem, "1.0.0", now));
        using var wrong = RSA.Create(3072);
        Assert.Throws<CryptographicException>(() => AgentRelease.Verify(Sign(release), wrong.ExportSubjectPublicKeyInfoPem(), "1.0.0", now));
    }
    [Theory]
    [InlineData("../IAM.Agent.exe")]
    [InlineData("/IAM.Agent.exe")]
    [InlineData("IAM.Agent.exe:payload")]
    [InlineData("update.ps1")]
    public void Archive_rejects_everything_except_the_single_executable(string entry)
    {
        var root = Path.Combine(Path.GetTempPath(), "iam-agent-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var zip = Path.Combine(root, "package.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            { using var writer = new StreamWriter(archive.CreateEntry(entry).Open()); writer.Write("test"); }
            Assert.Throws<InvalidDataException>(() => AgentRelease.ExtractPackage(zip, Path.Combine(root, "version")));
            Assert.False(Directory.Exists(Path.Combine(root, "version")));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Package_hash_and_size_are_verified_before_extraction()
    {
        var root = Path.Combine(Path.GetTempPath(), "iam-agent-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var zip = Path.Combine(root, "package.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            { using var writer = new StreamWriter(archive.CreateEntry("IAM.Agent.exe").Open()); writer.Write("verified bytes"); }
            var bytes = await File.ReadAllBytesAsync(zip, TestContext.Current.CancellationToken);
            var release = new AgentRelease("IAM.Agent", 1, "1.0.0", "win-x64", "IAM.Agent-1.0.0-win-x64.zip", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), DateTimeOffset.UtcNow.AddDays(1), "1.0.0");
            await release.VerifyPackage(zip, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<CryptographicException>(() => (release with { Sha256 = new string('0', 64) }).VerifyPackage(zip, TestContext.Current.CancellationToken));
            AgentRelease.ExtractPackage(zip, Path.Combine(root, "version"));
            Assert.Equal("verified bytes", await File.ReadAllTextAsync(Path.Combine(root, "version", "IAM.Agent.exe"), TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, true); }
    }
}
