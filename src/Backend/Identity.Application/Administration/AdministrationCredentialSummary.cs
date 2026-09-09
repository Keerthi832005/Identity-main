namespace Identity.Application.Administration;

public sealed record AdministrationCredentialSummary(
    long UserCredentialId,
    string CredentialType,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? RevokedAt);
