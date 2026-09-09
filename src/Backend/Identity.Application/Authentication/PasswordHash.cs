namespace Identity.Application.Authentication;

public sealed record PasswordHash(
    string Algorithm,
    int IterationCount,
    byte[] Salt,
    byte[] Hash);
