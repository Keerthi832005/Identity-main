using Identity.Application.Messaging;

namespace Identity.Application.Authentication;

public sealed record TerminalVerificationCommand(
    long TerminalId,
    string EmployeeCode,
    string Pin,
    string ClientId,
    string ClientSecret,
    Guid CorrelationId) : IRequest<TerminalVerificationResult>;

public enum TerminalVerificationFailureCode
{
    InvalidClient,
    InvalidCredentials,
    AccessDenied,
    TerminalNotTrusted,
    LockedOut,
    PinChangeRequired,
    InvalidNewPin,
}

public sealed record TerminalVerificationResult(
    bool Succeeded,
    TerminalVerificationFailureCode? FailureCode,
    long? UserId,
    string? EmployeeCode,
    string? DisplayName,
    IReadOnlyList<string>? CapabilityCodes = null)
{
    public static TerminalVerificationResult Rejected(
        TerminalVerificationFailureCode failureCode) => new(
            false, failureCode, null, null, null);

    public static TerminalVerificationResult Verified(
        long userId, string employeeCode, string displayName,
        IReadOnlyList<string>? capabilityCodes = null) => new(
            true, null, userId, employeeCode, displayName, capabilityCodes ?? []);
}
