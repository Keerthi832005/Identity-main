using Identity.Application.BulkData;

namespace Identity.Application.Administration;

/// <summary>Everything one batch resolved up front, so no validator issues a query per row.</summary>
public sealed record CatalogBatch(
    IReadOnlyList<BulkRow> Rows,
    IReadOnlyDictionary<string, long> Applications,
    IReadOnlyDictionary<long, IReadOnlyDictionary<string, long>> Modules,
    IReadOnlyDictionary<long, IReadOnlyDictionary<string, long>> Roles,
    IReadOnlyDictionary<long, IReadOnlyDictionary<string, long>> Capabilities)
{
    public bool ModuleExists(long applicationId, string code) =>
        Modules.TryGetValue(applicationId, out var codes) && codes.ContainsKey(code);

    public bool RoleExists(long applicationId, string code) =>
        Roles.TryGetValue(applicationId, out var codes) && codes.ContainsKey(code);

    public bool CapabilityExists(long applicationId, string code) =>
        Capabilities.TryGetValue(applicationId, out var codes) && codes.ContainsKey(code);
}
