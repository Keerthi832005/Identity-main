using System.Text.Json;
using System.Text.Json.Serialization;

namespace Identity.AdminCli;

public sealed record ApplicationProvisioningManifest(
    string ApplicationCode,
    string ApplicationName,
    string? Description,
    string Audience,
    int AccessTokenLifetimeMinutes,
    int RefreshTokenLifetimeDays,
    IReadOnlyList<PublicClientManifest> PublicClients,
    IReadOnlyList<ApplicationModuleManifest> Modules,
    IReadOnlyList<ApplicationRoleManifest> Roles)
{
    public static async Task<ApplicationProvisioningManifest> Load(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var stream = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<ApplicationProvisioningManifest>(
            stream,
            SerializerOptions,
            cancellationToken) ?? throw new ApplicationProvisioningException(
                "The application manifest cannot be empty.");
        manifest.Validate();
        return manifest;
    }

    public void Validate()
    {
        Required(ApplicationCode, nameof(ApplicationCode));
        Required(ApplicationName, nameof(ApplicationName));
        Required(Audience, nameof(Audience));
        if (AccessTokenLifetimeMinutes is < 1 or > 60)
        {
            throw new ApplicationProvisioningException(
                "Access token lifetime must be between 1 and 60 minutes.");
        }

        if (RefreshTokenLifetimeDays is < 1 or > 90)
        {
            throw new ApplicationProvisioningException(
                "Refresh token lifetime must be between 1 and 90 days.");
        }

        RequireCollection(PublicClients, nameof(PublicClients));
        RequireCollection(Modules, nameof(Modules));
        RequireCollection(Roles, nameof(Roles));
        foreach (var client in PublicClients)
        {
            Required(client.ClientId, nameof(client.ClientId));
            Required(client.ClientName, nameof(client.ClientName));
        }

        foreach (var module in Modules)
        {
            Required(module.ModuleCode, nameof(module.ModuleCode));
            Required(module.ModuleName, nameof(module.ModuleName));
            if (module.DisplayOrder < 0)
            {
                throw new ApplicationProvisioningException(
                    $"Module '{module.ModuleCode}' display order cannot be negative.");
            }

            RequireCollection(module.Capabilities, $"{module.ModuleCode}.Capabilities");
            foreach (var capability in module.Capabilities)
            {
                Required(capability.CapabilityCode, nameof(capability.CapabilityCode));
                Required(capability.CapabilityName, nameof(capability.CapabilityName));
            }
        }

        foreach (var role in Roles)
        {
            Required(role.RoleCode, nameof(role.RoleCode));
            Required(role.RoleName, nameof(role.RoleName));
            RequireCollection(role.CapabilityCodes, $"{role.RoleCode}.CapabilityCodes");
        }

        Unique(PublicClients.Select(client => client.ClientId), "client id");
        Unique(Modules.Select(module => module.ModuleCode), "module code");
        Unique(Modules.SelectMany(module => module.Capabilities).Select(capability => capability.CapabilityCode),
            "capability code");
        Unique(Roles.Select(role => role.RoleCode), "role code");

        var capabilityCodes = Modules
            .SelectMany(module => module.Capabilities)
            .Select(capability => capability.CapabilityCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var role in Roles)
        {
            Unique(role.CapabilityCodes, $"capability code in role '{role.RoleCode}'");
            foreach (var capabilityCode in role.CapabilityCodes)
            {
                if (!capabilityCodes.Contains(capabilityCode))
                {
                    throw new ApplicationProvisioningException(
                        $"Role '{role.RoleCode}' references unknown capability '{capabilityCode}'.");
                }
            }
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static void Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ApplicationProvisioningException($"Manifest value '{name}' is required.");
        }
    }

    private static void RequireCollection<T>(IReadOnlyList<T>? value, string name)
    {
        if (value is null || value.Count == 0)
        {
            throw new ApplicationProvisioningException(
                $"Manifest collection '{name}' must contain at least one item.");
        }
    }

    private static void Unique(IEnumerable<string> values, string description)
    {
        var duplicates = values
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new ApplicationProvisioningException(
                $"Manifest {description} values must be unique: {string.Join(", ", duplicates)}.");
        }
    }
}

public sealed record PublicClientManifest(string ClientId, string ClientName);

public sealed record ApplicationModuleManifest(
    string ModuleCode,
    string ModuleName,
    string? Description,
    int DisplayOrder,
    bool IsSystem,
    IReadOnlyList<ModuleCapabilityManifest> Capabilities);

public sealed record ModuleCapabilityManifest(
    string CapabilityCode,
    string CapabilityName,
    string? Description);

public sealed record ApplicationRoleManifest(
    string RoleCode,
    string RoleName,
    string? Description,
    bool IsSystem,
    IReadOnlyList<string> CapabilityCodes);
