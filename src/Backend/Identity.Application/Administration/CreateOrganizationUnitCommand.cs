using Identity.Domain.Entities;
using Identity.Application.Messaging;
using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed record CreateOrganizationUnitCommand(
    long OrganizationId,
    long ParentOrganizationUnitId,
    OrganizationUnitType UnitType,
    string Code,
    string Name,
    AdministrationContext Context,
    string? Description = null, OrganizationUnitAddress? Address = null) : IRequest<OrganizationHierarchyResult>;
