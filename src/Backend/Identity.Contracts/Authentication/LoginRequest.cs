using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Authentication;

public sealed record LoginRequest(
    [property: Required, StringLength(100, MinimumLength = 1)] string EmployeeCode,
    [property: Required, StringLength(1024, MinimumLength = 12)] string Password,
    [property: Required, StringLength(150, MinimumLength = 1)] string ClientId,
    [property: StringLength(1024)] string? ClientSecret,
    long? DeviceId);
