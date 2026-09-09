using Identity.Application.BulkData;

namespace Identity.Application.Administration;

/// <summary>
/// Bulk templates for access grants: which people can reach which application, and which roles they
/// hold there. Every reference is a natural code, so an administrator never types a surrogate id.
///
/// Permission overrides are deliberately absent. An override is a targeted exception with a reason
/// and an expiry, granted one at a time after a decision; a spreadsheet of them is a sign the roles
/// are wrong, not a workflow to make faster.
/// </summary>
public static class AccessGrantBulkDescriptors
{
    public const string EmployeeCode = "employeeCode";
    public const string ApplicationCode = "applicationCode";
    public const string RoleCode = "roleCode";

    public const string UserApplicationsKey = "user-applications";
    public const string UserRolesKey = "user-roles";

    private static BulkColumnDefinition Employee() => new(
        EmployeeCode,
        "Employee code",
        BulkColumnType.Text,
        Required: true,
        WidthInCharacters: 20,
        HelpText: "The person receiving access. They must already exist.",
        ReferenceListKey: "users",
        MaxLength: 50);

    private static BulkColumnDefinition Application() => new(
        ApplicationCode,
        "Application code",
        BulkColumnType.Text,
        Required: true,
        WidthInCharacters: 24,
        HelpText: "The application the access applies to.",
        ReferenceListKey: "applications",
        MaxLength: 100);

    public static BulkEntityDescriptor UserApplications { get; } = new(
        UserApplicationsKey,
        "Application access",
        1,
        [Employee(), Application()],
        "One grant per row. Granting access does not assign any role; use the roles template for that.",
        KeyColumnIds: [EmployeeCode, ApplicationCode]);

    public static BulkEntityDescriptor UserRoles { get; } = new(
        UserRolesKey,
        "Role assignments",
        1,
        [
            Employee(),
            Application(),
            new BulkColumnDefinition(
                RoleCode,
                "Role code",
                BulkColumnType.Text,
                Required: true,
                WidthInCharacters: 24,
                HelpText: "A role belonging to the named application.",
                ReferenceListKey: "roles",
                MaxLength: 100),
        ],
        "One assignment per row. The person must already have access to the application.",
        KeyColumnIds: [EmployeeCode, ApplicationCode, RoleCode]);
}
