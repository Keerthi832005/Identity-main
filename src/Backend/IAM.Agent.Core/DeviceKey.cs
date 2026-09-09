using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Identity.Contracts.Agents;

namespace IAM.Agent.Core;

[SupportedOSPlatform("windows")]
public static class DeviceKey
{
    public static AgentEnrollment CreateOrRead(string directory)
    {
        Directory.CreateDirectory(directory);
        var identityPath = Path.Combine(directory, "identity.json");
        var keyPath = Path.Combine(directory, "device-key.dpapi");
        if (File.Exists(identityPath))
        {
            var existing = JsonSerializer.Deserialize<AgentEnrollment>(File.ReadAllText(identityPath), AgentProtocol.Json)!;
            using var key = Read(directory);
            if (existing.PublicKey != Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()))
                throw new InvalidDataException("Installed device key does not match its identity.");
            return existing with { Hostname = Environment.MachineName };
        }
        if (File.Exists(keyPath)) throw new InvalidDataException("Incomplete identity installation; restore identity.json from backup.");
        using var generated = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKey = generated.ExportPkcs8PrivateKey();
        try { File.WriteAllBytes(keyPath, ProtectedData.Protect(privateKey, null, DataProtectionScope.LocalMachine)); }
        finally { CryptographicOperations.ZeroMemory(privateKey); }
        var identity = new AgentEnrollment(Guid.NewGuid(), Environment.MachineName, Convert.ToBase64String(generated.ExportSubjectPublicKeyInfo()));
        AtomicJson.Write(identityPath, identity);
        return identity;
    }

    public static ECDsa Read(string directory)
    {
        var decrypted = ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(directory, "device-key.dpapi")), null, DataProtectionScope.LocalMachine);
        var key = ECDsa.Create();
        try { key.ImportPkcs8PrivateKey(decrypted, out _); return key; }
        catch { key.Dispose(); throw; }
        finally { CryptographicOperations.ZeroMemory(decrypted); }
    }
}

public static class AtomicJson
{
    public static void Write<T>(string path, T value)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, value, AgentSettings.Json);
            stream.Flush(true);
        }
        File.Move(temporary, path, true);
    }
}
