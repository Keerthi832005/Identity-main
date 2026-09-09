using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Authentication;

public sealed record TerminalVerificationRequest(
    [property: Range(1, long.MaxValue)] long TerminalId,
    [property: Required, StringLength(100, MinimumLength = 1)] string EmployeeCode,
    [property: Required, RegularExpression("^[0-9]{4}$")] string Pin,
    [property: Required, StringLength(150, MinimumLength = 1)] string ClientId,
    [property: Required, StringLength(1024, MinimumLength = 16)] string ClientSecret);

public sealed record TerminalVerificationResponse(
    bool Succeeded,
    string? FailureCode,
    long? UserId,
    string? EmployeeCode,
    string? DisplayName,
    IReadOnlyList<string>? CapabilityCodes = null);
