namespace Identity.AdminCli;

public sealed record ProvisionedResource(string Code, long ResourceId, bool Created);

public sealed record ApplicationProvisioningResult(
    ProvisionedResource Application,
    IReadOnlyList<ProvisionedResource> PublicClients,
    IReadOnlyList<ProvisionedResource> Modules,
    IReadOnlyList<ProvisionedResource> Capabilities,
    IReadOnlyList<ProvisionedResource> Roles,
    int CreatedRolePermissions,
    int ExistingRolePermissions);
