namespace Identity.Domain.Entities;

public sealed class Role
{
    private Role()
    {
    }

    public static Role Create(
        long applicationId,
        string roleCode,
        string roleName,
        string? description,
        bool isSystem,
        DateTime createdAt) => new()
        {
            ApplicationId = DomainRules.Positive(applicationId, nameof(applicationId)),
            RoleCode = DomainRules.Required(roleCode, nameof(roleCode), 50),
            RoleName = DomainRules.Required(roleName, nameof(roleName), 100),
            Description = DomainRules.Optional(description, nameof(description), 500),
            IsSystem = isSystem,
            IsActive = true,
            CreatedAt = createdAt,
        };

    public void SetActive(bool isActive, DateTime updatedAt)
    {
        IsActive = isActive;
        UpdatedAt = updatedAt;
    }

    public long RoleId { get; private set; }
    public long ApplicationId { get; private set; }
    public string RoleCode { get; private set; } = string.Empty;
    public string RoleName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}
