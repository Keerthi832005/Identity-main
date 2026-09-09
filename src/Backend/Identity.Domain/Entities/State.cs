using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>Typed hierarchy link. Shared details and concurrency live on OrganizationUnit.</summary>
public sealed class State
{
    private State() { }

    public static State Create(OrganizationUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (unit.OrganizationUnitId <= 0 || unit.UnitType != OrganizationUnitType.State || unit.ParentOrganizationUnitId is null)
            throw new ArgumentException("A persisted State organization unit is required.", nameof(unit));
        return new State
        {
            StateId = unit.OrganizationUnitId,
            OrganizationUnitId = unit.OrganizationUnitId,
            OrganizationId = unit.OrganizationId,
            RegionId = unit.ParentOrganizationUnitId!.Value,
            Unit = unit,
        };
    }

    public long StateId { get; private set; }
    public long OrganizationId { get; private set; }
    public long OrganizationUnitId { get; private set; }
    public OrganizationUnitType UnitType { get; private set; } = OrganizationUnitType.State;
    public long RegionId { get; private set; }
    public Region Region { get; private set; } = null!;
    public OrganizationUnit Unit { get; private set; } = null!;
}
