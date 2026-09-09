using Identity.Application.Messaging;
using Identity.Domain.Entities;

namespace Identity.Application.Administration;

public sealed record UpdateOrganizationUnitCommand(long OrganizationId, long OrganizationUnitId,
 string Code, string Name, string? Description, OrganizationUnitAddress Address, byte[] RowVersion, AdministrationContext Context)
 : IRequest<OrganizationUnitDetails>;
