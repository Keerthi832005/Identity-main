using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record RegisterDeviceRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string DeviceName,
    [property: Required, StringLength(50, MinimumLength = 1)] string DeviceType,
    [property: Required, StringLength(1000, MinimumLength = 8)] string DeviceFingerprint);
