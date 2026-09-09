using Identity.Application.BulkData;

namespace Identity.Application.Administration;

/// <summary>
/// Bulk templates for the application catalog: modules, capabilities and roles. These are the
/// highest-volume seeding tasks in the product.
///
/// The application itself is deliberately absent. Creating one fixes a token audience and token
/// lifetimes, which is a considered one-off act rather than a row in a spreadsheet, and client
/// secrets must never travel through a template.
/// </summary>
public static class CatalogBulkDescriptors
{
    public const string ApplicationCode = "applicationCode";
    public const string ModuleCode = "moduleCode";
    public const string ParentModuleCode = "parentModuleCode";
    public const string CapabilityCode = "capabilityCode";
    public const string RoleCode = "roleCode";
    public const string Name = "name";
    public const string Description = "description";
    public const string DisplayOrder = "displayOrder";
    public const string IsSystem = "isSystem";

    public const string ModulesKey = "modules";
    public const string CapabilitiesKey = "capabilities";
    public const string RolesKey = "roles";

    private static BulkColumnDefinition Application() => new(
        ApplicationCode,
        "Application code",
        BulkColumnType.Text,
        Required: true,
        WidthInCharacters: 22,
        HelpText: "The application this row belongs to. It must already exist.",
        ReferenceListKey: "applications",
        MaxLength: 100);

    private static BulkColumnDefinition SystemFlag() => new(
        IsSystem,
        "System",
        BulkColumnType.Boolean,
        Required: false,
        WidthInCharacters: 10,
        HelpText: "Yes marks it as built in and protected from casual edits.",
        AllowedValues: ["Yes", "No"]);

    public static BulkEntityDescriptor Modules { get; } = new(
        ModulesKey,
        "Modules",
        1,
        [
            Application(),
            new BulkColumnDefinition(
                ModuleCode, "Module code", BulkColumnType.Text, Required: true,
                WidthInCharacters: 22, MaxLength: 100),
            new BulkColumnDefinition(
                Name, "Module name", BulkColumnType.Text, Required: true,
                WidthInCharacters: 28, MaxLength: 200),
            new BulkColumnDefinition(
                Description, "Description", BulkColumnType.Text, Required: false,
                WidthInCharacters: 34, MaxLength: 500),
            new BulkColumnDefinition(
                ParentModuleCode, "Parent module code", BulkColumnType.Text, Required: false,
                WidthInCharacters: 22,
                HelpText: "Leave blank for a top-level module. A parent may be created by an earlier row in this file.",
                MaxLength: 100),
            new BulkColumnDefinition(
                DisplayOrder, "Display order", BulkColumnType.Number, Required: false,
                WidthInCharacters: 14),
            SystemFlag(),
        ],
        "One module per row. A parent module may be defined in this same file; order does not matter.",
        KeyColumnIds: [ApplicationCode, ModuleCode]);

    public static BulkEntityDescriptor Capabilities { get; } = new(
        CapabilitiesKey,
        "Capabilities",
        1,
        [
            Application(),
            new BulkColumnDefinition(
                ModuleCode, "Module code", BulkColumnType.Text, Required: true,
                WidthInCharacters: 22,
                HelpText: "The module that owns this capability.",
                MaxLength: 100),
            new BulkColumnDefinition(
                CapabilityCode, "Capability code", BulkColumnType.Text, Required: true,
                WidthInCharacters: 28,
                HelpText: "Unique across every application, not just this one.",
                MaxLength: 150),
            new BulkColumnDefinition(
                Name, "Capability name", BulkColumnType.Text, Required: true,
                WidthInCharacters: 28, MaxLength: 200),
            new BulkColumnDefinition(
                Description, "Description", BulkColumnType.Text, Required: false,
                WidthInCharacters: 34, MaxLength: 500),
        ],
        "One capability per row. The module must already exist or be created before this import.",
        KeyColumnIds: [CapabilityCode]);

    public static BulkEntityDescriptor Roles { get; } = new(
        RolesKey,
        "Roles",
        1,
        [
            Application(),
            new BulkColumnDefinition(
                RoleCode, "Role code", BulkColumnType.Text, Required: true,
                WidthInCharacters: 24, MaxLength: 100),
            new BulkColumnDefinition(
                Name, "Role name", BulkColumnType.Text, Required: true,
                WidthInCharacters: 28, MaxLength: 200),
            new BulkColumnDefinition(
                Description, "Description", BulkColumnType.Text, Required: false,
                WidthInCharacters: 34, MaxLength: 500),
            SystemFlag(),
        ],
        "One role per row. Capability grants are managed on the role itself, not in this file.",
        KeyColumnIds: [ApplicationCode, RoleCode]);
}
