namespace Identity.Domain.Entities;

public sealed class ModuleCapability
{
    private ModuleCapability()
    {
    }

    public static ModuleCapability Create(
        long applicationId,
        long applicationModuleId,
        string capabilityCode,
        string capabilityName,
        string? description,
        DateTime createdAt) => new()
        {
            ApplicationId = DomainRules.Positive(applicationId, nameof(applicationId)),
            ApplicationModuleId = DomainRules.Positive(applicationModuleId, nameof(applicationModuleId)),
            CapabilityCode = ValidateCapabilityCode(capabilityCode),
            CapabilityName = DomainRules.Required(capabilityName, nameof(capabilityName), 150),
            Description = DomainRules.Optional(description, nameof(description), 500),
            IsActive = true,
            CreatedAt = createdAt,
        };

    public void SetActive(bool isActive, DateTime updatedAt)
    {
        IsActive = isActive;
        UpdatedAt = updatedAt;
    }

    public long ModuleCapabilityId { get; private set; }
    public long ApplicationId { get; private set; }
    public long ApplicationModuleId { get; private set; }
    public string CapabilityCode { get; private set; } = string.Empty;
    public string CapabilityName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private static string ValidateCapabilityCode(string capabilityCode)
    {
        var value = DomainRules.Required(capabilityCode, nameof(capabilityCode), 150);
        if (!value.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Capability codes must use the module.action format.",
                nameof(capabilityCode));
        }

        return value;
    }
}
