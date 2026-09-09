using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record CreateCapabilityRequest(
    [property: Required, StringLength(150, MinimumLength = 1)] string CapabilityCode,
    [property: Required, StringLength(200, MinimumLength = 1)] string CapabilityName,
    [property: StringLength(1000)] string? Description);
