namespace Identity.Application.Administration;

public sealed record AdministrationModuleSummary(
    long ApplicationModuleId,
    long ApplicationId,
    string ModuleCode,
    string ModuleName,
    string? Description,
    long? ParentApplicationModuleId,
    int DisplayOrder,
    bool IsSystem,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
