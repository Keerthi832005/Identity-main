using Identity.Application.BulkData;

namespace Identity.Application.Administration;

/// <summary>
/// The users bulk template. Deliberately limited to profile fields: credentials, PINs, MFA and
/// device trust are never bulk-writable and never appear as a column.
/// </summary>
public static class UsersBulkDescriptor
{
    public const string EntityKey = "users";

    public const string EmployeeCode = "employeeCode";
    public const string DisplayName = "displayName";
    public const string Email = "email";
    public const string ManagerEmployeeCode = "managerEmployeeCode";
    public const string BranchCode = "branchCode";

    public static BulkEntityDescriptor Descriptor { get; } = new(
        EntityKey,
        "Users",
        2,
        [
            new BulkColumnDefinition(
                EmployeeCode,
                "Employee code",
                BulkColumnType.Text,
                Required: true,
                WidthInCharacters: 18,
                HelpText: "The code the person signs in with. Must be unique.",
                MaxLength: 50),
            new BulkColumnDefinition(
                DisplayName,
                "Display name",
                BulkColumnType.Text,
                Required: true,
                WidthInCharacters: 28,
                HelpText: "Shown in the directory and on every audit event.",
                MaxLength: 200),
            new BulkColumnDefinition(
                Email,
                "Contact email",
                BulkColumnType.Text,
                Required: false,
                WidthInCharacters: 32,
                HelpText: "Optional. Contact only - it does not enable email sign-in or recovery.",
                MaxLength: 256),
            new BulkColumnDefinition(
                ManagerEmployeeCode,
                "Manager employee code",
                BulkColumnType.Text,
                Required: false,
                WidthInCharacters: 22,
                HelpText:
                    "The manager's employee code. They may be created earlier in this same file.",
                ReferenceListKey: "managers",
                MaxLength: 50),
            new BulkColumnDefinition(
                BranchCode,
                "Branch code",
                BulkColumnType.Text,
                Required: false,
                WidthInCharacters: 20,
                HelpText: "Optional employee branch. State, region and country are derived automatically.",
                ReferenceListKey: "organization-units",
                MaxLength: 50),
        ],
        "One user per row. Employee code and display name are required. "
        + "Branch is optional. Passwords, PINs and MFA are never set here.",
        KeyColumnIds: [EmployeeCode]);
}
