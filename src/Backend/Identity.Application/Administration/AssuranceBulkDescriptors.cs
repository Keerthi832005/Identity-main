using Identity.Application.BulkData;

namespace Identity.Application.Administration;

/// <summary>
/// Export-only templates for the assurance screens.
///
/// Neither entity is importable, and that is enforced by <see cref="BulkEntityDescriptor.AllowsImport"/>
/// rather than by leaving a committer unregistered. The audit trail is append-only, and credential,
/// PIN, MFA, device-trust and session-revocation operations are irreversible security acts: a
/// spreadsheet row is the wrong control surface for any of them. Refusing at the descriptor means a
/// future screen cannot make them writable simply by wiring up a toolbar.
/// </summary>
public static class AssuranceBulkDescriptors
{
    public const string AuditKey = "audit-events";
    public const string SessionsKey = "sessions";

    public static BulkEntityDescriptor AuditEvents { get; } = new(
        AuditKey,
        "Audit events",
        1,
        [
            new BulkColumnDefinition("occurredAt", "Occurred at", BulkColumnType.Date, false, 22),
            new BulkColumnDefinition("eventType", "Event", BulkColumnType.Text, false, 26),
            new BulkColumnDefinition("succeeded", "Succeeded", BulkColumnType.Boolean, false, 12),
            new BulkColumnDefinition("failureCode", "Failure code", BulkColumnType.Text, false, 22),
            new BulkColumnDefinition("employeeCode", "Employee code", BulkColumnType.Text, false, 18),
            new BulkColumnDefinition("applicationCode", "Application", BulkColumnType.Text, false, 22),
            new BulkColumnDefinition("correlationId", "Correlation id", BulkColumnType.Text, false, 38),
        ],
        "Export only. The audit trail is append-only and cannot be imported or edited.",
        AllowsImport: false);

    public static BulkEntityDescriptor Sessions { get; } = new(
        SessionsKey,
        "Sessions",
        1,
        [
            new BulkColumnDefinition("tokenFamilyId", "Session family", BulkColumnType.Text, false, 38),
            new BulkColumnDefinition("employeeCode", "Employee code", BulkColumnType.Text, false, 18),
            new BulkColumnDefinition("applicationCode", "Application", BulkColumnType.Text, false, 22),
            new BulkColumnDefinition("issuedAt", "Issued at", BulkColumnType.Date, false, 22),
            new BulkColumnDefinition("expiresAt", "Expires at", BulkColumnType.Date, false, 22),
            new BulkColumnDefinition("isActive", "Active", BulkColumnType.Boolean, false, 12),
        ],
        "Export only. Revoking a session is an irreversible security act and stays a single-record operation.",
        AllowsImport: false);
}
