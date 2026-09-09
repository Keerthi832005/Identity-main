namespace Identity.Contracts.Administration;

public sealed record EffectiveCapabilitiesResponse(
    int AuthorizationVersion,
    IReadOnlyList<string> CapabilityCodes);
