using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record CreateApplicationRequest(
    [property: Required, StringLength(100, MinimumLength = 1)] string ApplicationCode,
    [property: Required, StringLength(200, MinimumLength = 1)] string ApplicationName,
    [property: StringLength(1000)] string? Description,
    [property: Required, StringLength(500, MinimumLength = 1)] string TokenAudience,
    [property: Range(1, 60)] int AccessTokenLifetimeMinutes,
    [property: Range(1, 90)] int RefreshTokenLifetimeDays);
