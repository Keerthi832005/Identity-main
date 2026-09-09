using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record GetAdministrationApplicationCatalogQuery(long ApplicationId)
    : IRequest<AdministrationApplicationCatalog>;
