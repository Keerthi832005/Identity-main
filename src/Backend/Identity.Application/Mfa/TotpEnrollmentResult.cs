namespace Identity.Application.Mfa;

public sealed record TotpEnrollmentResult(
    long UserMfaMethodId,
    string Secret,
    string Algorithm,
    int Digits,
    int PeriodSeconds);
