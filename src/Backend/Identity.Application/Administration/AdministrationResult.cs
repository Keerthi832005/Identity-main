namespace Identity.Application.Administration;

public sealed record AdministrationResult(
    string ResourceType,
    long ResourceId,
    long? UserId,
    long? ApplicationId,
    int? AuthorizationVersion);
