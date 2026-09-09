namespace Identity.Contracts.Administration;

public sealed record ApplicationClientSummaryResponse(
    long ApplicationClientId,
    long ApplicationId,
    string ClientId,
    string ClientName,
    string ClientType,
    int SecretVersion,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? RevokedAt);
