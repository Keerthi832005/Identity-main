namespace Identity.Contracts.Administration;

public sealed record AdministrationResponse(
    string ResourceType,
    long ResourceId,
    long? UserId,
    long? ApplicationId,
    int? AuthorizationVersion);
