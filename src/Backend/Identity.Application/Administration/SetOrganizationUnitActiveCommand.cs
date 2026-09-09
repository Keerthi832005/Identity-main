using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record SetOrganizationUnitActiveCommand(long OrganizationId, long OrganizationUnitId,
 bool IsActive, byte[] RowVersion, AdministrationContext Context) : IRequest<OrganizationUnitDetails>;
