namespace Identity.Application.Administration;

public sealed record AdministrationClientSummary(
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
