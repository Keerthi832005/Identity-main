using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Authentication;

public sealed record LogoutRequest(
    [property: Required, StringLength(512, MinimumLength = 32)] string RefreshToken);
