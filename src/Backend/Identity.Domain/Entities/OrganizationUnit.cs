using System.Globalization;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class OrganizationUnit
{
    private OrganizationUnit() { }

    public static OrganizationUnit CreateRoot(long organizationId, string code, string name, DateTime createdAt) =>
        New(organizationId, null, OrganizationUnitType.Organization, code, name, createdAt);

    public static OrganizationUnit CreateChild(long organizationId, OrganizationUnit parent,
        OrganizationUnitType type, string code, string name, DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (parent.OrganizationUnitId <= 0 || parent.OrganizationId != organizationId || !parent.IsActive
            || ParentType(type) != parent.UnitType)
        {
            throw new ArgumentException("Parent must be a persisted active unit of the expected type in the same organization.", nameof(parent));
        }

        return New(organizationId, parent.OrganizationUnitId, type, code, name, createdAt);
    }

    public static OrganizationUnitType? ParentType(OrganizationUnitType type) => type switch
    {
        OrganizationUnitType.Organization => null,
        OrganizationUnitType.Country or OrganizationUnitType.Department => OrganizationUnitType.Organization,
        OrganizationUnitType.Region => OrganizationUnitType.Country,
        OrganizationUnitType.State => OrganizationUnitType.Region,
        OrganizationUnitType.Branch => OrganizationUnitType.State,
        OrganizationUnitType.Location => OrganizationUnitType.Branch,
        OrganizationUnitType.Team => OrganizationUnitType.Department,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public void InitializeHierarchyPath(OrganizationUnit? parent)
    {
        if (OrganizationUnitId <= 0) throw new InvalidOperationException("Persist the unit before initializing its path.");
        if (UnitType == OrganizationUnitType.Organization)
        {
            if (parent is not null) throw new ArgumentException("A root cannot have a parent.", nameof(parent));
        }
        else if (parent is null || parent.OrganizationUnitId != ParentOrganizationUnitId
            || parent.OrganizationId != OrganizationId || string.IsNullOrWhiteSpace(parent.HierarchyPath))
        {
            throw new ArgumentException("A matching parent with an initialized path is required.", nameof(parent));
        }

        var path = (parent?.HierarchyPath ?? "/") + OrganizationUnitId.ToString(CultureInfo.InvariantCulture) + "/";
        if (path.Length > 450) throw new InvalidOperationException("Hierarchy path exceeds the supported storage length.");
        if (HierarchyPath is not null && HierarchyPath != path)
            throw new InvalidOperationException("Hierarchy paths cannot be reassigned by this workflow.");
        HierarchyPath = path;
    }

    public void UpdateDetails(string code, string name, string? description, DateTime updatedAt)
    {
        UnitCode = DomainRules.Required(code, nameof(code), 50);
        UnitName = DomainRules.Required(name, nameof(name), 200);
        Description = DomainRules.Optional(description, nameof(description), 500);
        UpdatedAt = updatedAt;
    }

    public void SetAddress(OrganizationUnitAddress address, DateTime updatedAt)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.Latitude is < -90 or > 90 || address.Longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(address), "Coordinates are outside their valid range.");
        AddressLine1 = DomainRules.Optional(address.AddressLine1, nameof(address.AddressLine1), 250);
        AddressLine2 = DomainRules.Optional(address.AddressLine2, nameof(address.AddressLine2), 250);
        AddressLine3 = DomainRules.Optional(address.AddressLine3, nameof(address.AddressLine3), 250);
        City = DomainRules.Optional(address.City, nameof(address.City), 100);
        District = DomainRules.Optional(address.District, nameof(address.District), 100);
        StateName = DomainRules.Optional(address.StateName, nameof(address.StateName), 100);
        PostalCode = DomainRules.Optional(address.PostalCode, nameof(address.PostalCode), 20);
        CountryCode = DomainRules.Optional(address.CountryCode, nameof(address.CountryCode), 3);
        Latitude = address.Latitude;
        Longitude = address.Longitude;
        UpdatedAt = updatedAt;
    }

    public void SetActive(bool isActive, DateTime updatedAt)
    {
        IsActive = isActive;
        UpdatedAt = updatedAt;
    }

    private static OrganizationUnit New(long organizationId, long? parentId, OrganizationUnitType type,
        string code, string name, DateTime createdAt) => new()
        {
            OrganizationId = DomainRules.Positive(organizationId, nameof(organizationId)),
            ParentOrganizationUnitId = parentId,
            UnitType = type,
            UnitCode = DomainRules.Required(code, nameof(code), 50),
            UnitName = DomainRules.Required(name, nameof(name), 200),
            IsActive = true,
            CreatedAt = createdAt,
        };

    public long OrganizationUnitId { get; private set; }
    public long OrganizationId { get; private set; }
    public long? ParentOrganizationUnitId { get; private set; }
    public string? HierarchyPath { get; private set; }
    public OrganizationUnitType UnitType { get; private set; }
    public string UnitCode { get; private set; } = string.Empty;
    public string UnitName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string? AddressLine3 { get; private set; }
    public string? City { get; private set; }
    public string? District { get; private set; }
    public string? StateName { get; private set; }
    public string? PostalCode { get; private set; }
    public string? CountryCode { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
