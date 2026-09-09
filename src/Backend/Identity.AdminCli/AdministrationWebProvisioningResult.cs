namespace Identity.AdminCli;

public sealed record AdministrationWebProvisioningResult(
    long ApplicationId,
    long ApplicationClientId,
    long UserId,
    long RoleId,
    bool ApplicationAccessCreated,
    bool RoleAssignmentCreated);
