using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;

namespace Identity.Application.Mfa;

public sealed record VerifyTotpEnrollmentCommand(
    long UserMfaMethodId,
    string Code,
    AdministrationContext Context) : IRequest<OperationResult>;
