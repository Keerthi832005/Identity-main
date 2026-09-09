using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class AuthenticationAudit
{
    private AuthenticationAudit()
    {
    }

    public static AuthenticationAudit CreateAdministrationEvent(
        AdministrationAuditEventType eventType,
        Guid correlationId,
        DateTime occurredAt,
        long? userId = null,
        long? applicationId = null,
        long? applicationClientId = null,
        long? deviceId = null,
        string? eventDataJson = null) => new()
        {
            UserId = userId,
            ApplicationId = applicationId,
            ApplicationClientId = applicationClientId,
            EventType = eventType.ToString(),
            Succeeded = true,
            DeviceId = deviceId,
            CorrelationId = correlationId,
            OccurredAt = occurredAt,
            EventDataJson = eventDataJson,
        };

    public static AuthenticationAudit CreateAuthenticationEvent(
        AuthenticationAuditEventType eventType,
        bool succeeded,
        string? failureCode,
        Guid correlationId,
        DateTime occurredAt,
        long? userId = null,
        long? applicationId = null,
        long? applicationClientId = null,
        long? deviceId = null,
        byte[]? loginIdentifierHash = null,
        string? eventDataJson = null) => new()
        {
            UserId = userId,
            ApplicationId = applicationId,
            ApplicationClientId = applicationClientId,
            EventType = eventType.ToString(),
            Succeeded = succeeded,
            FailureCode = failureCode,
            DeviceId = deviceId,
            LoginIdentifierHash = loginIdentifierHash is null
            ? null
            : DomainRules.Hash(loginIdentifierHash, nameof(loginIdentifierHash), 32),
            CorrelationId = correlationId,
            OccurredAt = occurredAt,
            EventDataJson = eventDataJson,
        };

    public long AuthenticationAuditId { get; private set; }
    public long? UserId { get; private set; }
    public long? ApplicationId { get; private set; }
    public long? ApplicationClientId { get; private set; }
    public byte[]? LoginIdentifierHash { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public bool Succeeded { get; private set; }
    public string? FailureCode { get; private set; }
    public long? DeviceId { get; private set; }
    public byte[]? ClientAddressHash { get; private set; }
    public byte[]? UserAgentHash { get; private set; }
    public Guid CorrelationId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string? EventDataJson { get; private set; }
}
