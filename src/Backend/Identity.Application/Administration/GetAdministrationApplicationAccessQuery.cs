using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record GetAdministrationApplicationAccessQuery(long ApplicationId)
    : IRequest<AdministrationApplicationAccessCatalog>;
