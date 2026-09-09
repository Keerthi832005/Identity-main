namespace Identity.Contracts.Administration;

public sealed record AdministrationResourceResponse(
    string ResourceKind,
    long ResourceId,
    long? UserId,
    long? ApplicationId,
    string? Code,
    string? Name,
    bool? IsActive,
    int? AuthorizationVersion,
    DateTime? RevokedAt,
    string? PermissionEffect);
