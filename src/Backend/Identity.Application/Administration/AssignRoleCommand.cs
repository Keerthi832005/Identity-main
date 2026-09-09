using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record AssignRoleCommand(
    long UserId,
    long ApplicationId,
    long RoleId,
    AdministrationContext Context) : IRequest<AdministrationResult>;
