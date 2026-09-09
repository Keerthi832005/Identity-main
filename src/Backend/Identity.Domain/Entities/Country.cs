using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>Typed hierarchy link. Shared details and concurrency live on OrganizationUnit.</summary>
public sealed class Country
{
    private Country() { }

    public static Country Create(OrganizationUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (unit.OrganizationUnitId <= 0 || unit.UnitType != OrganizationUnitType.Country)
            throw new ArgumentException("A persisted Country organization unit is required.", nameof(unit));
        return new Country
        {
            CountryId = unit.OrganizationUnitId,
            OrganizationUnitId = unit.OrganizationUnitId,
            OrganizationId = unit.OrganizationId,
            Unit = unit,
        };
    }

    public long CountryId { get; private set; }
    public long OrganizationId { get; private set; }
    public long OrganizationUnitId { get; private set; }
    public OrganizationUnitType UnitType { get; private set; } = OrganizationUnitType.Country;
    public OrganizationUnit Unit { get; private set; } = null!;
}
