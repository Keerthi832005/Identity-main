namespace Identity.Contracts.Administration;

public sealed record ModuleCapabilitySummaryResponse(
    long ModuleCapabilityId,
    long ApplicationId,
    long ApplicationModuleId,
    string CapabilityCode,
    string CapabilityName,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
