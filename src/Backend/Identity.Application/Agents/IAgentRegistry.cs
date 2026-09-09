using Identity.Application.Administration;
using Identity.Contracts.Agents;

namespace Identity.Application.Agents;

public sealed record AgentSummary(Guid InstallationId, long DeviceId, string Hostname, string? AgentVersion,
    DateTime CreatedAt, DateTime? LastReportAt, bool IsActive, bool IsTrusted, DateTime? TrustedUntil,
    string? CurrentUserName = null, bool WindowsUsersReported = false);
public sealed record AgentDetails(AgentSummary Machine, MachineInventory? Inventory);
public sealed record AgentPage(IReadOnlyList<AgentSummary> Items, int Total, int Skip, int Take);
public interface IAgentRegistry
{
    Task<AgentEnrollmentResult> Enroll(AgentEnrollment request, AdministrationContext context, CancellationToken ct);
    Task<bool> Report(SignedMachineReport report, CancellationToken ct);
    Task<AgentPage> Search(string? search, int skip, int take, CancellationToken ct);
    Task<AgentDetails?> Get(Guid id, CancellationToken ct);
}
