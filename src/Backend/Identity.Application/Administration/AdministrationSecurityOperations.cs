namespace Identity.Application.Administration;

public sealed record AdministrationSecurityOperations(
    int Skip,
    int Take,
    int TotalAuditCount,
    IReadOnlyList<AdministrationAuditSummary> Audits,
    IReadOnlyList<AdministrationSessionSummary> Sessions,
    DateTime GeneratedAt);
