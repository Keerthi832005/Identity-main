namespace Identity.Application.Authentication;

public sealed record AuthenticationSecurityPolicy(
    int MaximumFailedAttempts,
    TimeSpan LockoutDuration,
    TimeSpan DeviceTrustDuration)
{
    public static AuthenticationSecurityPolicy Default { get; } = new(
        5,
        TimeSpan.FromMinutes(15),
        TimeSpan.FromDays(30));

    public AuthenticationSecurityPolicy Validate()
    {
        if (MaximumFailedAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(MaximumFailedAttempts));
        if (LockoutDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(LockoutDuration));
        if (DeviceTrustDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(DeviceTrustDuration));
        return this;
    }
}
