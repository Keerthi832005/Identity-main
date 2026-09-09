using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record GrantRolePermissionCommand(
    long ApplicationId,
    long RoleId,
    long ModuleCapabilityId,
    AdministrationContext Context) : IRequest<AdministrationResult>;
