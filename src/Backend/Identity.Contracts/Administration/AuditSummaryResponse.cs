namespace Identity.Contracts.Administration;

public sealed record AuditSummaryResponse(
    long AuthenticationAuditId,
    long? UserId,
    long? ApplicationId,
    string EventType,
    bool Succeeded,
    string? FailureCode,
    Guid CorrelationId,
    DateTime OccurredAt,
    string? UserDisplayName = null,
    string? EmployeeCode = null,
    string? ApplicationName = null);
