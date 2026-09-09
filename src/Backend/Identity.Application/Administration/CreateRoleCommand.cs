using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record CreateRoleCommand(
    long ApplicationId,
    string RoleCode,
    string RoleName,
    string? Description,
    bool IsSystem,
    AdministrationContext Context) : IRequest<AdministrationResult>;
