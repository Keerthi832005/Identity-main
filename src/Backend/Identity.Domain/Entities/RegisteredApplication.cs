namespace Identity.Domain.Entities;

public sealed class RegisteredApplication
{
    private RegisteredApplication()
    {
    }

    public static RegisteredApplication Create(
        string applicationCode,
        string applicationName,
        string? description,
        string tokenAudience,
        int accessTokenLifetimeMinutes,
        int refreshTokenLifetimeDays,
        DateTime createdAt)
    {
        if (accessTokenLifetimeMinutes is < 1 or > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(accessTokenLifetimeMinutes));
        }

        if (refreshTokenLifetimeDays is < 1 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshTokenLifetimeDays));
        }

        return new RegisteredApplication
        {
            ApplicationCode = DomainRules.Required(applicationCode, nameof(applicationCode), 100),
            ApplicationName = DomainRules.Required(applicationName, nameof(applicationName), 150),
            Description = DomainRules.Optional(description, nameof(description), 500),
            TokenAudience = DomainRules.Required(tokenAudience, nameof(tokenAudience), 200),
            AccessTokenLifetimeMinutes = accessTokenLifetimeMinutes,
            RefreshTokenLifetimeDays = refreshTokenLifetimeDays,
            IsActive = true,
            CreatedAt = createdAt,
        };
    }

    public void SetActive(bool isActive, DateTime updatedAt)
    {
        IsActive = isActive;
        UpdatedAt = updatedAt;
    }

    public long ApplicationId { get; private set; }
    public string ApplicationCode { get; private set; } = string.Empty;
    public string ApplicationName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string TokenAudience { get; private set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; private set; }
    public int RefreshTokenLifetimeDays { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}
