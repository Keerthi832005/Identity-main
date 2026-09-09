using Identity.Application.BulkData;
using Identity.Domain.Enums;

namespace Identity.Application.Administration;

/// <summary>
/// The organization units bulk template. All eight types, both hierarchy branches, and the shared
/// address block. Moves and hard deletion stay out, as they did in T020-T024.
/// </summary>
public static class OrganizationBulkDescriptor
{
    public const string EntityKey = "organization-units";

    public const string UnitType = "unitType";
    public const string UnitCode = "unitCode";
    public const string UnitName = "unitName";
    public const string ParentUnitCode = "parentUnitCode";
    public const string Description = "description";
    public const string AddressLine1 = "addressLine1";
    public const string AddressLine2 = "addressLine2";
    public const string AddressLine3 = "addressLine3";
    public const string City = "city";
    public const string District = "district";
    public const string StateName = "stateName";
    public const string PostalCode = "postalCode";
    public const string CountryCode = "countryCode";
    public const string Latitude = "latitude";
    public const string Longitude = "longitude";

    /// <summary>The eight approved types, in hierarchy order so the dropdown reads top-down.</summary>
    public static IReadOnlyList<string> Types { get; } =
    [
        nameof(OrganizationUnitType.Organization),
        nameof(OrganizationUnitType.Country),
        nameof(OrganizationUnitType.Region),
        nameof(OrganizationUnitType.State),
        nameof(OrganizationUnitType.Branch),
        nameof(OrganizationUnitType.Location),
        nameof(OrganizationUnitType.Department),
        nameof(OrganizationUnitType.Team),
    ];

    public static BulkEntityDescriptor Descriptor { get; } = new(
        EntityKey,
        "Organization units",
        1,
        [
            new BulkColumnDefinition(
                UnitType, "Unit type", BulkColumnType.Enumeration, Required: true,
                WidthInCharacters: 16,
                HelpText: "One of the eight approved types. The parent's type decides what is allowed here.",
                AllowedValues: Types),
            new BulkColumnDefinition(
                UnitCode, "Unit code", BulkColumnType.Text, Required: true,
                WidthInCharacters: 18,
                HelpText: "Unique across the whole organization, including other unit types.",
                MaxLength: 50),
            new BulkColumnDefinition(
                UnitName, "Unit name", BulkColumnType.Text, Required: true,
                WidthInCharacters: 28, MaxLength: 200),
            new BulkColumnDefinition(
                ParentUnitCode, "Parent unit code", BulkColumnType.Text, Required: false,
                WidthInCharacters: 18,
                HelpText: "Blank only for an Organization row. A parent may be created by another row in this file.",
                MaxLength: 50),
            new BulkColumnDefinition(
                Description, "Description", BulkColumnType.Text, Required: false,
                WidthInCharacters: 30, MaxLength: 500),
            new BulkColumnDefinition(AddressLine1, "Address line 1", BulkColumnType.Text, false, 26, MaxLength: 250),
            new BulkColumnDefinition(AddressLine2, "Address line 2", BulkColumnType.Text, false, 26, MaxLength: 250),
            new BulkColumnDefinition(AddressLine3, "Address line 3", BulkColumnType.Text, false, 26, MaxLength: 250),
            new BulkColumnDefinition(City, "City", BulkColumnType.Text, false, 18, MaxLength: 100),
            new BulkColumnDefinition(District, "District", BulkColumnType.Text, false, 18, MaxLength: 100),
            new BulkColumnDefinition(StateName, "State", BulkColumnType.Text, false, 18, MaxLength: 100),
            new BulkColumnDefinition(PostalCode, "Postal code", BulkColumnType.Text, false, 14, MaxLength: 20),
            new BulkColumnDefinition(CountryCode, "Country code", BulkColumnType.Text, false, 14, MaxLength: 3),
            new BulkColumnDefinition(Latitude, "Latitude", BulkColumnType.Number, false, 14),
            new BulkColumnDefinition(Longitude, "Longitude", BulkColumnType.Number, false, 14),
        ],
        "One unit per row. A parent may appear anywhere in this file; order does not matter. "
        + "Leave the parent blank only on an Organization row.",
        KeyColumnIds: [UnitCode]);
}
