using Identity.Application.Administration;
using Identity.Contracts.Agents;

namespace Identity.Application.Agents;

public sealed record AgentUpdateStatus(Guid RequestId, DateTimeOffset RequestedAt, DateTimeOffset ExpiresAt,
    DateTimeOffset? DeliveredAt, DateTimeOffset? CompletedAt, string Status, string? AgentVersion);
public sealed record AgentControlStatus(DateTimeOffset? LastSeenAt, string? AgentVersion, string? SupervisorVersion,
    AgentUpdateStatus? Update, AgentUpdateStatus? Collect = null);
public interface IAgentControlRegistry
{
    Task<AgentControlStatus?> Get(Guid id, CancellationToken ct);
    Task<AgentUpdateStatus?> RequestUpdate(Guid id, AdministrationContext actor, CancellationToken ct);
    /// <summary>Asks the machine for an inventory report now instead of waiting for its 30 minute cycle.</summary>
    Task<AgentUpdateStatus?> RequestCollect(Guid id, AdministrationContext actor, CancellationToken ct);
    Task<AgentControlReply?> Poll(SignedMachineReport proof, CancellationToken ct);
    /// <summary>The worker's own signed poll: it delivers and acknowledges collection commands.</summary>
    Task<AgentCollectReply?> PollCollect(SignedMachineReport proof, CancellationToken ct);
}
