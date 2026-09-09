namespace Identity.AdminCli;

public sealed record ApplicationManifestSummary(
    string ApplicationCode,
    string Audience,
    int PublicClientCount,
    int ModuleCount,
    int CapabilityCount,
    int RoleCount,
    int RoleCapabilityGrantCount)
{
    public static ApplicationManifestSummary From(ApplicationProvisioningManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        manifest.Validate();
        return new ApplicationManifestSummary(
            manifest.ApplicationCode,
            manifest.Audience,
            manifest.PublicClients.Count,
            manifest.Modules.Count,
            manifest.Modules.Sum(module => module.Capabilities.Count),
            manifest.Roles.Count,
            manifest.Roles.Sum(role => role.CapabilityCodes.Count));
    }
}
