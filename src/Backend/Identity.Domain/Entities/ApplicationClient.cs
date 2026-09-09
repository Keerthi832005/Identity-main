using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public sealed class ApplicationClient
{
    private ApplicationClient()
    {
    }

    public static ApplicationClient Create(
        long applicationId,
        string clientId,
        string clientName,
        ApplicationClientType clientType,
        byte[]? clientSecretHash,
        DateTime createdAt,
        DateTime? expiresAt)
    {
        if (clientType == ApplicationClientType.Public && clientSecretHash is not null)
        {
            throw new ArgumentException("Public clients cannot store a secret hash.", nameof(clientSecretHash));
        }

        if (clientType != ApplicationClientType.Public && clientSecretHash is null)
        {
            throw new ArgumentException("Confidential and service clients require a secret hash.", nameof(clientSecretHash));
        }

        return new ApplicationClient
        {
            ApplicationId = DomainRules.Positive(applicationId, nameof(applicationId)),
            ClientId = DomainRules.Required(clientId, nameof(clientId), 150),
            ClientName = DomainRules.Required(clientName, nameof(clientName), 150),
            ClientType = clientType,
            ClientSecretHash = clientSecretHash is null
                ? null
                : DomainRules.Hash(clientSecretHash, nameof(clientSecretHash), 32),
            SecretVersion = clientSecretHash is null ? 0 : 1,
            IsActive = true,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
        };
    }

    public void Revoke(DateTime revokedAt)
    {
        IsActive = false;
        RevokedAt ??= revokedAt;
    }

    public long ApplicationClientId { get; private set; }
    public long ApplicationId { get; private set; }
    public string ClientId { get; private set; } = string.Empty;
    public string ClientName { get; private set; } = string.Empty;
    public ApplicationClientType ClientType { get; private set; }
    public byte[]? ClientSecretHash { get; private set; }
    public int SecretVersion { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
}
