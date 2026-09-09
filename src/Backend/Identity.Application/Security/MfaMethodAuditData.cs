namespace Identity.Application.Security;

public sealed record MfaMethodAuditData(
    long UserMfaMethodId,
    string MethodType,
    string Action) : IAuditPayload;
