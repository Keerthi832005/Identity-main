using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Authentication;

public sealed record RefreshTokenRequest(
    [property: Required, StringLength(512, MinimumLength = 32)] string RefreshToken,
    [property: Required, StringLength(150, MinimumLength = 1)] string ClientId,
    [property: StringLength(1024)] string? ClientSecret);
