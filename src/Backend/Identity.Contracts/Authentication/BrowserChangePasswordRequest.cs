using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Authentication;

public sealed record BrowserChangePasswordRequest(
    [property: Required, StringLength(150, MinimumLength = 1)] string ClientId,
    [property: Required, StringLength(1024, MinimumLength = 12)] string CurrentPassword,
    [property: Required, StringLength(1024, MinimumLength = 12)] string NewPassword);
