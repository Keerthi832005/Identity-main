using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed record AdministrationResourceResult(
    AdministrationResourceKind ResourceKind,
    long ResourceId,
    long? UserId,
    long? ApplicationId,
    string? Code,
    string? Name,
    bool? IsActive,
    int? AuthorizationVersion,
    DateTime? RevokedAt,
    PermissionEffect? PermissionEffect);
