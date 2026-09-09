using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record CreateApplicationClientRequest(
    [property: Required, StringLength(150, MinimumLength = 1)] string ClientId,
    [property: Required, StringLength(150, MinimumLength = 1)] string ClientName,
    [property: Required, RegularExpression("^(Public|Confidential|Service)$")] string ClientType,
    [property: StringLength(1024)] string? ClientSecret,
    DateTime? ExpiresAt);
