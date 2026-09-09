using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record PermissionOverrideRequest(
    [property: Required, RegularExpression("^(Allow|Deny)$")] string Effect,
    [property: Required, StringLength(500, MinimumLength = 1)] string Reason,
    DateTime? ExpiresAt);
