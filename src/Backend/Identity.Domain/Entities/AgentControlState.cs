namespace Identity.Domain.Entities;

public sealed class AgentControlState
{
    public Guid InstallationId { get; set; }
    public DateTime LastSignedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string AgentVersion { get; set; } = "";
    public string SupervisorVersion { get; set; } = "";
}

public sealed class AgentCollectState
{
    public Guid InstallationId { get; set; }
    public DateTime LastSignedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string AgentVersion { get; set; } = "";
}

public sealed class AgentUpdateRequest
{
    /// <summary>"update" checks for a signed worker release; "collect" asks for an inventory report now.</summary>
    public string Kind { get; set; } = "update";
    public Guid RequestId { get; set; }
    public Guid InstallationId { get; set; }
    public long RequestedByUserId { get; set; }
    public Guid CorrelationId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? AgentVersion { get; set; }
}
