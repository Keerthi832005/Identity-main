using System.Text.Json;
using Identity.Application.Messaging;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed class OrganizationManagementHandler(IOrganizationStore store, IAdministrationAuthorizer authorizer,
 ITransactionRunner transactionRunner, TimeProvider timeProvider) :
 IRequestHandler<SearchOrganizationUnitsQuery, PagedOrganizationUnits>,
 IRequestHandler<GetOrganizationUnitQuery, OrganizationUnitDetails>,
 IRequestHandler<UpdateOrganizationUnitCommand, OrganizationUnitDetails>,
 IRequestHandler<SetOrganizationUnitActiveCommand, OrganizationUnitDetails>
{
    public async ValueTask<PagedOrganizationUnits> Handle(SearchOrganizationUnitsQuery request, CancellationToken cancellationToken)
    {
        await authorizer.Authorize(request.Context, AdministrationAction.ReadOrganization, cancellationToken);
        if (request.Skip < 0 || request.Take is < 1 or > 50 || request.Search?.Length > 100
         || request.OrganizationId is <= 0 || request.ParentOrganizationUnitId is <= 0
         || request.UnitType.HasValue && !Enum.IsDefined(request.UnitType.Value)) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.ParentOrganizationUnitId.HasValue)
        {
            if (!request.OrganizationId.HasValue) throw new ArgumentException("Parent filtering requires an organization.");
            var parent = await store.Read(request.OrganizationId.Value, request.ParentOrganizationUnitId.Value, cancellationToken)
             ?? throw new KeyNotFoundException("Parent unit was not found in this organization.");
            if (request.UnitType.HasValue && OrganizationUnit.ParentType(request.UnitType.Value) != parent.UnitType)
                throw new ArgumentException("The filter type does not belong under this parent.");
        }
        return await store.Search(request with { Search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim() }, cancellationToken);
    }
    public async ValueTask<OrganizationUnitDetails> Handle(GetOrganizationUnitQuery request, CancellationToken cancellationToken)
    {
        await authorizer.Authorize(request.Context, AdministrationAction.ReadOrganization, cancellationToken);
        ValidateIds(request.OrganizationId, request.OrganizationUnitId);
        return await store.Read(request.OrganizationId, request.OrganizationUnitId, cancellationToken)
         ?? throw new KeyNotFoundException("Organization unit was not found.");
    }
    public ValueTask<OrganizationUnitDetails> Handle(UpdateOrganizationUnitCommand request, CancellationToken cancellationToken) =>
    new(transactionRunner.Execute(async token =>
    {
        await authorizer.Authorize(request.Context, AdministrationAction.UpdateOrganizationUnit, token);
        var unit = await ForUpdate(request.OrganizationId, request.OrganizationUnitId, request.RowVersion, token);
        var before = OrganizationUnitMapping.Details(unit);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var validated = OrganizationUnit.CreateRoot(request.OrganizationId, request.Code, request.Name, now);
        validated.UpdateDetails(request.Code, request.Name, request.Description, now);
        validated.SetAddress(request.Address, now);
        if (before.UnitCode == validated.UnitCode && before.UnitName == validated.UnitName && before.Description == validated.Description
      && before.Address == OrganizationUnitMapping.Address(validated)) return before;
        unit.UpdateDetails(validated.UnitCode, validated.UnitName, validated.Description, now);
        unit.SetAddress(OrganizationUnitMapping.Address(validated), now);
        Audit(AdministrationAuditEventType.OrganizationUnitUpdated, unit, request.Context, now);
        await store.SaveChanges(token);
        return await store.Read(unit.OrganizationId, unit.OrganizationUnitId, token)
            ?? throw new InvalidOperationException("Saved organization unit was not found.");
    }, cancellationToken));
    public ValueTask<OrganizationUnitDetails> Handle(SetOrganizationUnitActiveCommand request, CancellationToken cancellationToken) =>
    new(transactionRunner.Execute(async token =>
    {
        await authorizer.Authorize(request.Context, AdministrationAction.UpdateOrganizationUnit, token);
        var unit = await ForUpdate(request.OrganizationId, request.OrganizationUnitId, request.RowVersion, token);
        if (unit.IsActive == request.IsActive) return OrganizationUnitMapping.Details(unit);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        unit.SetActive(request.IsActive, now);
        Audit(AdministrationAuditEventType.OrganizationUnitStateChanged, unit, request.Context, now);
        await store.SaveChanges(token);
        return await store.Read(unit.OrganizationId, unit.OrganizationUnitId, token)
            ?? throw new InvalidOperationException("Saved organization unit was not found.");
    }, cancellationToken));
    private async Task<OrganizationUnit> ForUpdate(long organizationId, long id, byte[] version, CancellationToken token)
    {
        ValidateIds(organizationId, id);
        if (version is not { Length: 8 }) throw new ArgumentException("An eight-byte RowVersion is required.");
        await store.LockOrganization(organizationId, token);
        var unit = await store.FindUnit(id, token);
        if (unit is null || unit.OrganizationId != organizationId) throw new KeyNotFoundException("Organization unit was not found.");
        if (!unit.RowVersion.AsSpan().SequenceEqual(version)) throw new OrganizationConcurrencyException();
        store.SetOriginalRowVersion(unit, version);
        return unit;
    }
    private void Audit(AdministrationAuditEventType type, OrganizationUnit unit, AdministrationContext context, DateTime now) =>
    store.Add(AuthenticationAudit.CreateAdministrationEvent(type, context.CorrelationId, now, userId: context.ActorUserId,
    eventDataJson: JsonSerializer.Serialize(new OrganizationStateAuditData(unit.OrganizationId, unit.OrganizationUnitId, unit.UnitType, unit.IsActive))));
    private static void ValidateIds(long organizationId, long id)
    {
        if (organizationId <= 0 || id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
    }
}
