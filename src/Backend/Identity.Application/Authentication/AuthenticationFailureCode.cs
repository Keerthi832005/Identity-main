namespace Identity.Application.Authentication;

public enum AuthenticationFailureCode
{
    InvalidClient,
    InvalidCredentials,
    AccountLocked,
    AccessDenied,
    InvalidRefreshToken,
    RefreshTokenExpired,
    RefreshTokenReused,
    SessionVersionStale,
    MfaInvalid,
    MfaExpired,
    MfaAttemptsExceeded,
}
