using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record CreateCapabilityCommand(
    long ApplicationId,
    long ApplicationModuleId,
    string CapabilityCode,
    string CapabilityName,
    string? Description,
    AdministrationContext Context) : IRequest<AdministrationResult>;
