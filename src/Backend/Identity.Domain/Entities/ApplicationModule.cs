namespace Identity.Domain.Entities;

public sealed class ApplicationModule
{
    private ApplicationModule()
    {
    }

    public static ApplicationModule Create(
        long applicationId,
        string moduleCode,
        string moduleName,
        string? description,
        long? parentApplicationModuleId,
        int displayOrder,
        bool isSystem,
        DateTime createdAt)
    {
        if (parentApplicationModuleId.HasValue)
        {
            DomainRules.Positive(parentApplicationModuleId.Value, nameof(parentApplicationModuleId));
        }

        if (displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayOrder));
        }

        return new ApplicationModule
        {
            ApplicationId = DomainRules.Positive(applicationId, nameof(applicationId)),
            ModuleCode = DomainRules.Required(moduleCode, nameof(moduleCode), 100),
            ModuleName = DomainRules.Required(moduleName, nameof(moduleName), 150),
            Description = DomainRules.Optional(description, nameof(description), 500),
            ParentApplicationModuleId = parentApplicationModuleId,
            DisplayOrder = displayOrder,
            IsSystem = isSystem,
            IsActive = true,
            CreatedAt = createdAt,
        };
    }

    /// <summary>
    /// Sets the module's position among its siblings. Whether a given module may be moved is an
    /// administration rule and is enforced where the move is decided; renumbering a sibling list has
    /// to be able to touch every member, including system modules that keep their relative place.
    /// </summary>
    public void SetDisplayOrder(int displayOrder, DateTime updatedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        if (DisplayOrder == displayOrder)
        {
            return;
        }

        DisplayOrder = displayOrder;
        UpdatedAt = updatedAt;
    }

    public void SetActive(bool isActive, DateTime updatedAt)
    {
        IsActive = isActive;
        UpdatedAt = updatedAt;
    }

    public long ApplicationModuleId { get; private set; }
    public long ApplicationId { get; private set; }
    public string ModuleCode { get; private set; } = string.Empty;
    public string ModuleName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public long? ParentApplicationModuleId { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}
