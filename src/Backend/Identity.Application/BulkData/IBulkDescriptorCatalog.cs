namespace Identity.Application.BulkData;

/// <summary>
/// The set of entities that support bulk data. An entity absent here is not merely unsupported: its
/// endpoints report not found, so no partially wired entity is ever reachable.
/// </summary>
public interface IBulkDescriptorCatalog
{
    bool TryResolve(string entityKey, out BulkEntityDefinition definition);
    IReadOnlyList<string> EntityKeys { get; }
}
