namespace Identity.AdminCli;

internal sealed record ProvisioningActor(
    long UserId,
    string AdministrationApplicationCode,
    string RequiredCapability);
