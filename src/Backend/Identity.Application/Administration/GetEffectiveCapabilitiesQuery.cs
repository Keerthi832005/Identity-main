using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record GetEffectiveCapabilitiesQuery(
    long UserId,
    long ApplicationId) : IRequest<EffectiveAuthorization>;
