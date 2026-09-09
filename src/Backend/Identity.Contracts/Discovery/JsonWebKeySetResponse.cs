namespace Identity.Contracts.Discovery;

public sealed record JsonWebKeySetResponse(IReadOnlyList<JsonWebKeyResponse> Keys);
