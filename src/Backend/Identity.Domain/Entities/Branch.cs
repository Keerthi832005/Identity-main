using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>Typed hierarchy link. Shared details and concurrency live on OrganizationUnit.</summary>
public sealed class Branch
{
    private Branch() { }

    public static Branch Create(OrganizationUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (unit.OrganizationUnitId <= 0 || unit.UnitType != OrganizationUnitType.Branch || unit.ParentOrganizationUnitId is null)
            throw new ArgumentException("A persisted Branch organization unit is required.", nameof(unit));
        return new Branch
        {
            BranchId = unit.OrganizationUnitId,
            OrganizationUnitId = unit.OrganizationUnitId,
            OrganizationId = unit.OrganizationId,
            StateId = unit.ParentOrganizationUnitId!.Value,
            Unit = unit,
        };
    }

    public long BranchId { get; private set; }
    public long OrganizationId { get; private set; }
    public long OrganizationUnitId { get; private set; }
    public OrganizationUnitType UnitType { get; private set; } = OrganizationUnitType.Branch;
    public long StateId { get; private set; }
    public State State { get; private set; } = null!;
    public OrganizationUnit Unit { get; private set; } = null!;
}
