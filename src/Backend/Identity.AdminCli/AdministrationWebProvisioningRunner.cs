using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.AdminCli;

public sealed class AdministrationWebProvisioningRunner(
    ApplicationProvisioningRunner applicationProvisioning,
    IRequestDispatcher dispatcher,
    IAdministrationStore store,
    IdentityDbContext dbContext)
{
    public async Task<AdministrationWebProvisioningResult> Run(
        ProvisionAdministrationWebOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var employeeCode = options.EmployeeCode.Trim();
        var user = await dbContext.UserAccounts.SingleOrDefaultAsync(
            value => value.EmployeeCode == employeeCode,
            cancellationToken) ?? throw new InvalidOperationException(
                "The requested administration user does not exist.");
        if (!user.IsActive)
        {
            throw new InvalidOperationException("The requested administration user is inactive.");
        }

        var manifest = await ResolveManifest(cancellationToken);
        var provisioned = await applicationProvisioning.Run(
            manifest,
            user.UserId,
            cancellationToken);
        var applicationId = provisioned.Application.ResourceId;
        var roleId = provisioned.Roles.Single().ResourceId;
        var context = new AdministrationContext(user.UserId, Guid.NewGuid());

        var accessCreated = false;
        var access = await store.FindUserApplication(user.UserId, applicationId, cancellationToken);
        if (access is null)
        {
            await dispatcher.Send(new GrantUserApplicationCommand(
                user.UserId,
                applicationId,
                context), cancellationToken);
            accessCreated = true;
        }
        else if (!access.IsActive)
        {
            throw new InvalidOperationException(
                "The existing administration application access is inactive.");
        }

        var assignmentCreated = false;
        if (!await store.HasActiveUserRole(
            user.UserId,
            applicationId,
            roleId,
            cancellationToken))
        {
            await dispatcher.Send(new AssignRoleCommand(
                user.UserId,
                applicationId,
                roleId,
                context), cancellationToken);
            assignmentCreated = true;
        }

        return new AdministrationWebProvisioningResult(
            applicationId,
            provisioned.PublicClients.Single().ResourceId,
            user.UserId,
            roleId,
            accessCreated,
            assignmentCreated);
    }

    private async Task<ApplicationProvisioningManifest> ResolveManifest(CancellationToken cancellationToken)
    {
        var application = await store.FindApplicationByCode(Manifest.ApplicationCode, cancellationToken);
        if (application is null)
        {
            return Manifest;
        }

        // The original bootstrap ships different descriptive text from web provisioning.
        // Preserve only these known legacy descriptions; all security and identity contract
        // checks remain enforced by ApplicationProvisioningRunner, including unknown drift.
        var capability = await store.FindCapabilityByCode("iam.admin", cancellationToken);
        var role = await store.FindRoleByCode(
            application.ApplicationId,
            "identity-administrator",
            cancellationToken);
        var module = Manifest.Modules.Single();
        var moduleCapability = module.Capabilities.Single();
        var applicationRole = Manifest.Roles.Single();
        return Manifest with
        {
            Description = application.Description == "Administration application created by the secure bootstrap command."
                ? application.Description
                : Manifest.Description,
            Modules =
            [
                module with
                {
                    Capabilities =
                    [
                        moduleCapability with
                        {
                            Description = capability?.Description == "Allows access to all Identity administration endpoints."
                                ? capability.Description
                                : moduleCapability.Description,
                        },
                    ],
                },
            ],
            Roles =
            [
                applicationRole with
                {
                    Description = role?.Description == "Bootstrap administration role."
                        ? role.Description
                        : applicationRole.Description,
                },
            ],
        };
    }

    private static readonly ApplicationProvisioningManifest Manifest = new(
        "iam-administration",
        "Identity Administration",
        "Independent Identity administration control plane.",
        "urn:identity:administration",
        15,
        7,
        [new PublicClientManifest("identity-admin-web", "Identity Administration Web")],
        [
            new ApplicationModuleManifest(
                "administration",
                "Administration",
                "Identity administration capabilities.",
                0,
                true,
                [
                    new ModuleCapabilityManifest(
                        "iam.admin",
                        "Identity administrator",
                        "Allows access to Identity administration endpoints."),
                ]),
        ],
        [
            new ApplicationRoleManifest(
                "identity-administrator",
                "Identity Administrator",
                "Full Identity administration role.",
                true,
                ["iam.admin"]),
        ]);
}
