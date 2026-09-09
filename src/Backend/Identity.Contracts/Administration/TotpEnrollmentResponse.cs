namespace Identity.Contracts.Administration;

public sealed record TotpEnrollmentResponse(
    long UserMfaMethodId,
    string Secret,
    string Algorithm,
    int Digits,
    int PeriodSeconds);
