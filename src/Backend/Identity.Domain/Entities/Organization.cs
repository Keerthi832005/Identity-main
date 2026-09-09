namespace Identity.Domain.Entities;

/// <summary>Ownership anchor. Business identity and active state live on its single root unit.</summary>
public sealed class Organization
{
    private Organization() { }

    public static Organization Create(DateTime createdAt) => new() { CreatedAt = createdAt };

    public long OrganizationId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
