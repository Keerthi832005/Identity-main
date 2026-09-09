using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>Typed hierarchy link. Shared details and concurrency live on OrganizationUnit.</summary>
public sealed class Team
{
    private Team() { }

    public static Team Create(OrganizationUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (unit.OrganizationUnitId <= 0 || unit.UnitType != OrganizationUnitType.Team || unit.ParentOrganizationUnitId is null)
            throw new ArgumentException("A persisted Team organization unit is required.", nameof(unit));
        return new Team
        {
            TeamId = unit.OrganizationUnitId,
            OrganizationUnitId = unit.OrganizationUnitId,
            OrganizationId = unit.OrganizationId,
            DepartmentId = unit.ParentOrganizationUnitId!.Value,
            Unit = unit,
        };
    }

    public long TeamId { get; private set; }
    public long OrganizationId { get; private set; }
    public long OrganizationUnitId { get; private set; }
    public OrganizationUnitType UnitType { get; private set; } = OrganizationUnitType.Team;
    public long DepartmentId { get; private set; }
    public OrganizationUnit Unit { get; private set; } = null!;
}
