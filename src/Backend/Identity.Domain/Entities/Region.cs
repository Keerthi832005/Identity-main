using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>Typed hierarchy link. Shared details and concurrency live on OrganizationUnit.</summary>
public sealed class Region
{
    private Region() { }

    public static Region Create(OrganizationUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (unit.OrganizationUnitId <= 0 || unit.UnitType != OrganizationUnitType.Region || unit.ParentOrganizationUnitId is null)
            throw new ArgumentException("A persisted Region organization unit is required.", nameof(unit));
        return new Region
        {
            RegionId = unit.OrganizationUnitId,
            OrganizationUnitId = unit.OrganizationUnitId,
            OrganizationId = unit.OrganizationId,
            CountryId = unit.ParentOrganizationUnitId!.Value,
            Unit = unit,
        };
    }

    public long RegionId { get; private set; }
    public long OrganizationId { get; private set; }
    public long OrganizationUnitId { get; private set; }
    public OrganizationUnitType UnitType { get; private set; } = OrganizationUnitType.Region;
    public long CountryId { get; private set; }
    public Country Country { get; private set; } = null!;
    public OrganizationUnit Unit { get; private set; } = null!;
}
