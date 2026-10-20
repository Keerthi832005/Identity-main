using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;
using Identity.Domain.Enums;
using Identity.Domain.Entities;

namespace Identity.AdminCli;

public sealed class BootstrapRunner(
    IRequestDispatcher dispatcher,
    ISecretHasher secretHasher,
    IBootstrapSession session)
{
    public Task<BootstrapResult> Run(
        BootstrapOptions options,
        string administratorPassword,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options, administratorPassword, clientSecret);
        return session.Execute(async (initialContext, token) =>
        {
            var application = await dispatcher.Send(new CreateApplicationCommand(
                options.ApplicationCode,
                options.ApplicationName,
                "Administration application created by the secure bootstrap command.",
                options.Audience,
                15,
                7,
                initialContext), token);
            var user = await session.CreateAdministrator(options, initialContext, token);
            var actorContext = new AdministrationContext(user.ResourceId, initialContext.CorrelationId);
            var client = await dispatcher.Send(new CreateApplicationClientCommand(
                application.ApplicationId!.Value,
                options.ClientId,
                "Bootstrap administration client",
                ApplicationClientType.Confidential,
                secretHasher.Hash(clientSecret),
                null,
                actorContext), token);
            var module = await dispatcher.Send(new CreateModuleCommand(
                application.ApplicationId.Value,
                "administration",
                "Administration",
                "Identity administration capabilities.",
                null,
                0,
                true,
                actorContext), token);
            var capability = await dispatcher.Send(new CreateCapabilityCommand(
                application.ApplicationId.Value,
                module.ResourceId,
                "iam.admin",
                "Identity administrator",
                "Allows access to all Identity administration endpoints.",
                actorContext), token);
            await dispatcher.Send(new GrantUserApplicationCommand(
                user.ResourceId,
                application.ApplicationId.Value,
                actorContext), token);
            var role = await dispatcher.Send(new CreateRoleCommand(
                application.ApplicationId.Value,
                "identity-administrator",
                "Identity Administrator",
                "Bootstrap administration role.",
                true,
                actorContext), token);
            await dispatcher.Send(new AssignRoleCommand(
                user.ResourceId,
                application.ApplicationId.Value,
                role.ResourceId,
                actorContext), token);
            await dispatcher.Send(new GrantRolePermissionCommand(
                application.ApplicationId.Value,
                role.ResourceId,
                capability.ResourceId,
                actorContext), token);
            await dispatcher.Send(new SetPasswordCommand(
                user.ResourceId,
                administratorPassword,
                null,
                actorContext), token);
            return new BootstrapResult(
                application.ApplicationId.Value,
                user.ResourceId,
                client.ResourceId,
                role.ResourceId,
                capability.ResourceId);
        }, cancellationToken);
    }

    private static void Validate(
        BootstrapOptions options,
        string administratorPassword,
        string clientSecret)
    {
        RequireLength(options.EmployeeCode, nameof(options.EmployeeCode), 1, 50);
        _ = UserAccount.ValidateEmail(options.Email);
        RequireLength(options.DisplayName, nameof(options.DisplayName), 1, 200);
        RequireLength(options.ApplicationCode, nameof(options.ApplicationCode), 1, 100);
        RequireLength(options.ApplicationName, nameof(options.ApplicationName), 1, 200);
        RequireLength(options.ClientId, nameof(options.ClientId), 1, 150);
        RequireLength(options.Audience, nameof(options.Audience), 1, 500);
        RequireLength(administratorPassword, nameof(administratorPassword), 12, 1024);
        RequireLength(clientSecret, nameof(clientSecret), 16, 1024);
    }

    private static void RequireLength(string value, string name, int minimum, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < minimum || value.Length > maximum)
        {
            throw new ArgumentOutOfRangeException(
                name,
                $"{name} must contain between {minimum} and {maximum} characters.");
        }
    }
}
