namespace Identity.Application.BulkData;

/// <summary>
/// Template contract for one bulk entity. <paramref name="KeyColumnIds"/> is the natural key used to
/// detect duplicates inside a batch and to decide create versus update.
///
/// <paramref name="AllowsImport"/> is false for a read-only entity such as the audit trail. That is
/// a structural exclusion, not a convention: staging refuses the entity outright, so no future
/// screen can accidentally make an append-only record writable by wiring up a toolbar.
/// </summary>
public sealed record BulkEntityDescriptor(
    string EntityKey,
    string DisplayName,
    int TemplateVersion,
    IReadOnlyList<BulkColumnDefinition> Columns,
    string Instruction,
    IReadOnlyList<string>? KeyColumnIds = null,
    bool AllowsImport = true);
