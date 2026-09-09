using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Identity.Application.Administration;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Infrastructure.Persistence;

internal sealed class OrganizationStore(IdentityDbContext dbContext) : IOrganizationStore, IOrganizationCodeResolver
{
    public async ValueTask<OrganizationUnit?> FindUnit(long organizationUnitId, CancellationToken cancellationToken)
    {
        var unit = await dbContext.OrganizationUnits.FindAsync([organizationUnitId], cancellationToken);
        // A dispatcher scope may issue multiple commands; refresh cached parent state after acquiring the organization lock.
        if (unit is not null) await dbContext.Entry(unit).ReloadAsync(cancellationToken);
        return unit;
    }

    public async Task LockOrganization(long organizationId, CancellationToken cancellationToken)
    {
        var resource = $"Identity.Organization.Management.{organizationId}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = {resource},
                @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 51009, 'Organization is busy. Retry the change.', 1;
            """, cancellationToken);
    }

    public async Task<PagedOrganizationUnits> Search(SearchOrganizationUnitsQuery query, CancellationToken cancellationToken)
    {
        var units = dbContext.OrganizationUnits.AsNoTracking().Where(unit => unit.HierarchyPath != null);
        if (query.OrganizationId.HasValue) units = units.Where(unit => unit.OrganizationId == query.OrganizationId.Value);
        if (query.UnitType.HasValue) units = units.Where(unit => unit.UnitType == query.UnitType.Value);
        if (query.ParentOrganizationUnitId.HasValue) units = units.Where(unit => unit.ParentOrganizationUnitId == query.ParentOrganizationUnitId.Value);
        if (query.IsActive.HasValue) units = units.Where(unit => unit.IsActive == query.IsActive.Value);
        if (query.Search is not null) units = units.Where(unit => unit.UnitCode.Contains(query.Search)
            || unit.UnitName.Contains(query.Search) || unit.Description != null && unit.Description.Contains(query.Search));
        var count = await units.CountAsync(cancellationToken);
        var page = await units.OrderBy(unit => unit.UnitName).ThenBy(unit => unit.OrganizationUnitId)
            .Skip(query.Skip).Take(query.Take).ToArrayAsync(cancellationToken);
        return new PagedOrganizationUnits(query.Skip, query.Take, count, page.Select(OrganizationUnitMapping.Details).ToArray());
    }

    public async Task<OrganizationUnitDetails?> Read(long organizationId, long organizationUnitId, CancellationToken cancellationToken)
    {
        var unit = await dbContext.OrganizationUnits.AsNoTracking().SingleOrDefaultAsync(unit =>
            unit.OrganizationId == organizationId && unit.OrganizationUnitId == organizationUnitId && unit.HierarchyPath != null, cancellationToken);
        return unit is null ? null : OrganizationUnitMapping.Details(unit);
    }

    public void SetOriginalRowVersion(OrganizationUnit unit, byte[] rowVersion) =>
        dbContext.Entry(unit).Property(value => value.RowVersion).OriginalValue = rowVersion;

    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new OrganizationConcurrencyException(); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new OrganizationCodeConflictException(); }
    }

    public void Add<TEntity>(TEntity entity) where TEntity : class => dbContext.Add(entity);

    public void AddTypedLink(OrganizationUnit unit)
    {
        object link = unit.UnitType switch
        {
            OrganizationUnitType.Country => Country.Create(unit),
            OrganizationUnitType.Region => Region.Create(unit),
            OrganizationUnitType.State => State.Create(unit),
            OrganizationUnitType.Branch => Branch.Create(unit),
            OrganizationUnitType.Location => Location.Create(unit),
            OrganizationUnitType.Department => Department.Create(unit),
            OrganizationUnitType.Team => Team.Create(unit),
            _ => throw new ArgumentOutOfRangeException(nameof(unit)),
        };
        dbContext.Add(link);
    }

    public async Task<IReadOnlyDictionary<string, OrganizationUnitRef>> FindUnitsByCodes(
        IReadOnlyCollection<string> unitCodes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitCodes);
        var normalized = unitCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
        {
            return new Dictionary<string, OrganizationUnitRef>(StringComparer.OrdinalIgnoreCase);
        }

        var matches = await dbContext.OrganizationUnits
            .Where(unit => normalized.Contains(EF.Property<string>(unit, "NormalizedUnitCode")))
            .Select(unit => new
            {
                unit.UnitCode,
                unit.OrganizationUnitId,
                unit.OrganizationId,
                unit.UnitType,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return matches.ToDictionary(
            match => match.UnitCode,
            match => new OrganizationUnitRef(
                match.OrganizationUnitId,
                match.OrganizationId,
                match.UnitType),
            StringComparer.OrdinalIgnoreCase);
    }
}
