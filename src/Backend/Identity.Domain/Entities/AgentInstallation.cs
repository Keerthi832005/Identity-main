namespace Identity.Domain.Entities;

public sealed class AgentInstallation
{
    private AgentInstallation() { }
    public static AgentInstallation Create(Guid id, long deviceId, string hostname, string publicKey, DateTime now) => new()
    {
        InstallationId = id != Guid.Empty ? id : throw new ArgumentException("Installation id required."),
        DeviceId = deviceId > 0 ? deviceId : throw new ArgumentException("Device id required."),
        Hostname = hostname,
        PublicKey = publicKey,
        CreatedAt = now,
    };
    public void Report(string hostname, string version, DateTime capturedAt, DateTime receivedAt, string inventoryJson)
    {
        if (LastCapturedAt >= capturedAt) throw new InvalidOperationException("An older or duplicate report cannot overwrite inventory.");
        Hostname = hostname; AgentVersion = version; LastCapturedAt = capturedAt; LastReportAt = receivedAt; InventoryJson = inventoryJson;
    }
    public Guid InstallationId { get; private set; }
    public long DeviceId { get; private set; }
    public string Hostname { get; private set; } = "";
    public string PublicKey { get; private set; } = "";
    public string? AgentVersion { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastCapturedAt { get; private set; }
    public DateTime? LastReportAt { get; private set; }
    public string? InventoryJson { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
