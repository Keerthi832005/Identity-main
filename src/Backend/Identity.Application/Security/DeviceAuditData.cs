namespace Identity.Application.Security;

public sealed record DeviceAuditData(long DeviceId, string Action) : IAuditPayload;
