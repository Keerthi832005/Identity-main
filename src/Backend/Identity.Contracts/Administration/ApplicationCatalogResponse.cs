namespace Identity.Contracts.Administration;

public sealed record ApplicationCatalogResponse(
    ApplicationSummaryResponse Application,
    IReadOnlyList<ApplicationClientSummaryResponse> Clients,
    IReadOnlyList<ApplicationModuleSummaryResponse> Modules,
    IReadOnlyList<ModuleCapabilitySummaryResponse> Capabilities);
