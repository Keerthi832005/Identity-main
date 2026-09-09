using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;

namespace Identity.Application.Mfa;

public sealed record RevokeMfaMethodCommand(
    long UserMfaMethodId,
    AdministrationContext Context) : IRequest<OperationResult>;
