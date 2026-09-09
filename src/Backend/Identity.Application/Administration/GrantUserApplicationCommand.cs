using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record GrantUserApplicationCommand(
    long UserId,
    long ApplicationId,
    AdministrationContext Context) : IRequest<AdministrationResult>;
