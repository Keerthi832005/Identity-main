using Identity.Domain.Entities;

namespace Identity.Application.Administration;

public static class OrganizationUnitMapping
{
    public static OrganizationUnitAddress Address(OrganizationUnit unit) => new(unit.AddressLine1, unit.AddressLine2,
    unit.AddressLine3, unit.City, unit.District, unit.StateName, unit.PostalCode, unit.CountryCode, unit.Latitude, unit.Longitude);
    public static OrganizationUnitDetails Details(OrganizationUnit unit) => new(unit.OrganizationId, unit.OrganizationUnitId,
    unit.ParentOrganizationUnitId, unit.UnitType, unit.UnitCode, unit.UnitName, unit.Description, Address(unit),
    unit.HierarchyPath ?? throw new InvalidOperationException("A committed unit must have a canonical path."),
    unit.IsActive, unit.CreatedAt, unit.UpdatedAt, unit.RowVersion.ToArray());
}
