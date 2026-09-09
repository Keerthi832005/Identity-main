namespace Identity.Application.Administration;

public sealed record AdministrationCapabilitySummary(
    long ModuleCapabilityId,
    long ApplicationId,
    long ApplicationModuleId,
    string CapabilityCode,
    string CapabilityName,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
