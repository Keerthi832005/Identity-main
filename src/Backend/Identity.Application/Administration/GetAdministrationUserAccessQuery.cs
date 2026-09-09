using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record GetAdministrationUserAccessQuery(long UserId)
    : IRequest<AdministrationUserAccessCatalog>;
