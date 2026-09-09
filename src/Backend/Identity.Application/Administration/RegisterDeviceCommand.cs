using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record RegisterDeviceCommand(
    long UserId,
    string DeviceName,
    string DeviceType,
    byte[] DeviceFingerprintHash,
    byte[]? UserAgentHash,
    byte[]? ClientAddressHash,
    AdministrationContext Context) : IRequest<AdministrationResult>;
