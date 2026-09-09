using Identity.Application.Messaging;
using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed record SearchOrganizationUnitsQuery(long? OrganizationId, OrganizationUnitType? UnitType,
 long? ParentOrganizationUnitId, bool? IsActive, string? Search, int Skip, int Take, AdministrationContext Context)
 : IRequest<PagedOrganizationUnits>;
