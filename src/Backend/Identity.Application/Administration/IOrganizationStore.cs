using Identity.Domain.Entities;

namespace Identity.Application.Administration;

public interface IOrganizationStore
{
    ValueTask<OrganizationUnit?> FindUnit(long organizationUnitId, CancellationToken cancellationToken);
    Task LockOrganization(long organizationId, CancellationToken cancellationToken);
    Task<PagedOrganizationUnits> Search(SearchOrganizationUnitsQuery query, CancellationToken cancellationToken);
    Task<OrganizationUnitDetails?> Read(long organizationId, long organizationUnitId, CancellationToken cancellationToken);
    void SetOriginalRowVersion(OrganizationUnit unit, byte[] rowVersion);
    Task SaveChanges(CancellationToken cancellationToken);
    void Add<TEntity>(TEntity entity) where TEntity : class;
    void AddTypedLink(OrganizationUnit unit);
}
