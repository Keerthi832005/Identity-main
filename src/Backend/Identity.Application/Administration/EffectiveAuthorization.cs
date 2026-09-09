namespace Identity.Application.Administration;

public sealed record EffectiveAuthorization(
    int AuthorizationVersion,
    IReadOnlyList<string> CapabilityCodes);
