using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record CreateModuleRequest(
    [property: Required, StringLength(100, MinimumLength = 1)] string ModuleCode,
    [property: Required, StringLength(200, MinimumLength = 1)] string ModuleName,
    [property: StringLength(1000)] string? Description,
    long? ParentApplicationModuleId,
    [property: Range(0, 10000)] int DisplayOrder,
    bool IsSystem);
