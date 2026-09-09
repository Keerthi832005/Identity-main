using Identity.Application.Administration;
using Identity.Application.Messaging;

namespace Identity.Application.Mfa;

public sealed record EnrollTotpCommand(
    long UserId,
    string MethodName,
    bool IsPrimary,
    AdministrationContext Context) : IRequest<TotpEnrollmentResult>;
