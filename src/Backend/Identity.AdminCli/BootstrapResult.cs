namespace Identity.AdminCli;

public sealed record BootstrapResult(
    long ApplicationId,
    long AdministratorUserId,
    long ApplicationClientId,
    long RoleId,
    long CapabilityId);
