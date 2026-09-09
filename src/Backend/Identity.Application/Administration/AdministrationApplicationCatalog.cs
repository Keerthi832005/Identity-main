namespace Identity.Application.Administration;

public sealed record AdministrationApplicationCatalog(
    AdministrationApplicationSummary Application,
    IReadOnlyList<AdministrationClientSummary> Clients,
    IReadOnlyList<AdministrationModuleSummary> Modules,
    IReadOnlyList<AdministrationCapabilitySummary> Capabilities);
