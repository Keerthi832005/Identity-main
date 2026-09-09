using Identity.Application.Messaging;

namespace Identity.Application.Authentication;

public sealed record LoginCommand(
    string EmployeeCode,
    string Password,
    string ClientId,
    string? ClientSecret,
    long? DeviceId,
    Guid CorrelationId) : IRequest<AuthenticationResult>;
