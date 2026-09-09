using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record CreateModuleCommand(
    long ApplicationId,
    string ModuleCode,
    string ModuleName,
    string? Description,
    long? ParentApplicationModuleId,
    int DisplayOrder,
    bool IsSystem,
    AdministrationContext Context) : IRequest<AdministrationResult>;
