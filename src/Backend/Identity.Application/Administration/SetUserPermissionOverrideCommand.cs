using Identity.Application.Messaging;
using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed record SetUserPermissionOverrideCommand(
    long UserId,
    long ApplicationId,
    long ModuleCapabilityId,
    PermissionEffect Effect,
    string Reason,
    DateTime? ExpiresAt,
    AdministrationContext Context) : IRequest<AdministrationResult>;
