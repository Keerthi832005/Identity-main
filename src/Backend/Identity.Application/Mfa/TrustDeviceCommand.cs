using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;

namespace Identity.Application.Mfa;

public sealed record TrustDeviceCommand(
    long DeviceId,
    AdministrationContext Context) : IRequest<OperationResult>;
