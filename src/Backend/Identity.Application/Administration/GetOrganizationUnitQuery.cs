using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record GetOrganizationUnitQuery(long OrganizationId, long OrganizationUnitId, AdministrationContext Context)
 : IRequest<OrganizationUnitDetails>;
