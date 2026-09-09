namespace Identity.Application.Administration;

public sealed record AdministrationUserSecurityCatalog(
    AdministrationUserSummary User,
    IReadOnlyList<AdministrationCredentialSummary> Credentials,
    IReadOnlyList<AdministrationDeviceSummary> Devices,
    IReadOnlyList<AdministrationMfaMethodSummary> MfaMethods);
