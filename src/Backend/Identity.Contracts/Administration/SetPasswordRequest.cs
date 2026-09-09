using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record SetPasswordRequest(
    [property: Required, StringLength(1024, MinimumLength = 12)] string Password,
    DateTime? ExpiresAt);
