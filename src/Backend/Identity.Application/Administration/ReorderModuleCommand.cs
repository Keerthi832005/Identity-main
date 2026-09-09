using Identity.Application.Messaging;

namespace Identity.Application.Administration;

/// <summary>
/// Moves a module one place up or down among its siblings. The caller says which direction rather
/// than supplying an index, so two administrators reordering at once cannot write conflicting
/// absolute positions.
/// </summary>
public sealed record ReorderModuleCommand(
    long ApplicationId,
    long ApplicationModuleId,
    bool MoveUp,
    AdministrationContext Context) : IRequest<AdministrationResult>;
