using System.Text.Json;
using Identity.Application.Messaging;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed class OrganizationCommandHandler(
    IOrganizationStore store,
    IAdministrationAuthorizer authorizer,
    ITransactionRunner transactionRunner,
    TimeProvider timeProvider) :
    IRequestHandler<CreateOrganizationCommand, OrganizationHierarchyResult>,
    IRequestHandler<CreateOrganizationUnitCommand, OrganizationHierarchyResult>
{
    public ValueTask<OrganizationHierarchyResult> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken) =>
        new(transactionRunner.Execute(async token =>
        {
            await authorizer.Authorize(request.Context, AdministrationAction.CreateOrganization, token);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var organization = Organization.Create(now);
            store.Add(organization);
            await store.SaveChanges(token);
            var root = OrganizationUnit.CreateRoot(organization.OrganizationId, request.Code, request.Name, now);
            root.UpdateDetails(request.Code, request.Name, request.Description, now);
            root.SetAddress(request.Address ?? new OrganizationUnitAddress(), now);
            store.Add(root);
            await store.SaveChanges(token);
            root.InitializeHierarchyPath(null);
            AddAudit(AdministrationAuditEventType.OrganizationCreated, root, request.Context, now);
            await store.SaveChanges(token);
            return Result(root);
        }, cancellationToken));

    public ValueTask<OrganizationHierarchyResult> Handle(CreateOrganizationUnitCommand request, CancellationToken cancellationToken) =>
        new(transactionRunner.Execute(async token =>
        {
            await authorizer.Authorize(request.Context, AdministrationAction.CreateOrganizationUnit, token);
            if (request.UnitType == OrganizationUnitType.Organization)
                throw new AdministrationException("Use organization creation to create its root atomically.");
            if (request.OrganizationId <= 0 || request.ParentOrganizationUnitId <= 0 || !Enum.IsDefined(request.UnitType))
                throw new ArgumentOutOfRangeException(nameof(request));
            await store.LockOrganization(request.OrganizationId, token);
            var parent = await store.FindUnit(request.ParentOrganizationUnitId, token)
                ?? throw new AdministrationException("Parent organization unit was not found.");
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var unit = OrganizationUnit.CreateChild(request.OrganizationId, parent, request.UnitType, request.Code, request.Name, now);
            unit.UpdateDetails(request.Code, request.Name, request.Description, now);
            unit.SetAddress(request.Address ?? new OrganizationUnitAddress(), now);
            store.Add(unit);
            await store.SaveChanges(token);
            unit.InitializeHierarchyPath(parent);
            store.AddTypedLink(unit);
            AddAudit(AdministrationAuditEventType.OrganizationUnitCreated, unit, request.Context, now);
            await store.SaveChanges(token);
            return Result(unit);
        }, cancellationToken));

    private void AddAudit(AdministrationAuditEventType type, OrganizationUnit unit, AdministrationContext context, DateTime now) =>
        store.Add(AuthenticationAudit.CreateAdministrationEvent(type, context.CorrelationId, now,
            userId: context.ActorUserId,
            eventDataJson: JsonSerializer.Serialize(new OrganizationAuditData(unit.OrganizationId, unit.OrganizationUnitId, unit.UnitType))));

    private static OrganizationHierarchyResult Result(OrganizationUnit unit) => new(
        unit.OrganizationId, unit.OrganizationUnitId, unit.UnitType,
        unit.HierarchyPath ?? throw new InvalidOperationException("Hierarchy path must be initialized before commit."),
        unit.RowVersion);
}
