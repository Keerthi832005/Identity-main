namespace Identity.Domain.Enums;

public enum AuthenticationAuditEventType
{
    LoginSucceeded,
    LoginFailed,
    TokenRefreshed,
    TokenRefreshRejected,
    LoggedOut,
    CredentialChanged,
    ClientAuthenticated,
    ClientAuthenticationFailed,
    MfaMethodAdded,
    MfaMethodVerified,
    MfaMethodVerificationRejected,
    MfaMethodRevoked,
    MfaChallengeCreated,
    MfaChallengeVerified,
    MfaChallengeRejected,
    DeviceTrusted,
    TerminalVerificationSucceeded,
    TerminalVerificationFailed,
}
