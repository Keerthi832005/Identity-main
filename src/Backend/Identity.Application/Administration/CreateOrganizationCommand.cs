using Identity.Domain.Entities;
using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record CreateOrganizationCommand(string Code, string Name, AdministrationContext Context,
    string? Description = null, OrganizationUnitAddress? Address = null)
    : IRequest<OrganizationHierarchyResult>;
