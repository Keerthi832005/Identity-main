using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record GetAdministrationUserSecurityQuery(long UserId)
    : IRequest<AdministrationUserSecurityCatalog>;
