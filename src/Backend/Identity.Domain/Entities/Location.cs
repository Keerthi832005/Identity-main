using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>Typed hierarchy link. Shared details and concurrency live on OrganizationUnit.</summary>
public sealed class Location
{
    private Location() { }

    public static Location Create(OrganizationUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (unit.OrganizationUnitId <= 0 || unit.UnitType != OrganizationUnitType.Location || unit.ParentOrganizationUnitId is null)
            throw new ArgumentException("A persisted Location organization unit is required.", nameof(unit));
        return new Location
        {
            LocationId = unit.OrganizationUnitId,
            OrganizationUnitId = unit.OrganizationUnitId,
            OrganizationId = unit.OrganizationId,
            BranchId = unit.ParentOrganizationUnitId!.Value,
            Unit = unit,
        };
    }

    public long LocationId { get; private set; }
    public long OrganizationId { get; private set; }
    public long OrganizationUnitId { get; private set; }
    public OrganizationUnitType UnitType { get; private set; } = OrganizationUnitType.Location;
    public long BranchId { get; private set; }
    public OrganizationUnit Unit { get; private set; } = null!;
}
