using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Configurations;

internal static class AgentModelConfiguration
{
    public static void Apply(ModelBuilder builder)
    {
        var agent = builder.Entity<AgentInstallation>();
        agent.ToTable("AgentInstallation", "Identity");
        agent.HasKey(a => a.InstallationId);
        agent.Property(a => a.Hostname).HasMaxLength(200);
        agent.Property(a => a.PublicKey).HasMaxLength(256);
        agent.Property(a => a.AgentVersion).HasMaxLength(50);
        agent.Property(a => a.RowVersion).IsRowVersion();
        agent.Property(a => a.LastCapturedAt).HasPrecision(7);
        agent.HasIndex(a => a.DeviceId).IsUnique();
        agent.HasOne<Device>().WithMany().HasForeignKey(a => a.DeviceId).OnDelete(DeleteBehavior.Restrict);
        var control = builder.Entity<AgentControlState>();
        control.ToTable("AgentControlState", "Identity"); control.HasKey(a => a.InstallationId);
        control.Property(a => a.LastSignedAt).HasPrecision(7);
        control.Property(a => a.LastSeenAt).HasPrecision(3);
        control.Property(a => a.AgentVersion).HasMaxLength(50); control.Property(a => a.SupervisorVersion).HasMaxLength(50);
        control.HasOne<AgentInstallation>().WithMany().HasForeignKey(a => a.InstallationId).OnDelete(DeleteBehavior.Restrict);
        var collect = builder.Entity<AgentCollectState>();
        collect.ToTable("AgentCollectState", "Identity"); collect.HasKey(a => a.InstallationId);
        collect.Property(a => a.LastSignedAt).HasPrecision(7);
        collect.Property(a => a.LastSeenAt).HasPrecision(3);
        collect.Property(a => a.AgentVersion).HasMaxLength(50);
        collect.HasOne<AgentInstallation>().WithMany().HasForeignKey(a => a.InstallationId).OnDelete(DeleteBehavior.Restrict);
        var update = builder.Entity<AgentUpdateRequest>();
        update.ToTable("AgentUpdateRequest", "Identity"); update.HasKey(a => a.RequestId);
        update.Property(a => a.RequestedAt).HasPrecision(3); update.Property(a => a.ExpiresAt).HasPrecision(3);
        update.Property(a => a.DeliveredAt).HasPrecision(3); update.Property(a => a.CompletedAt).HasPrecision(3);
        update.Property(a => a.Result).HasMaxLength(20); update.Property(a => a.AgentVersion).HasMaxLength(50);
        update.Property(a => a.Kind).HasMaxLength(20);
        update.HasIndex(a => new { a.InstallationId, a.RequestedAt });
        update.HasIndex(a => new { a.InstallationId, a.Kind, a.RequestedAt });
        update.HasOne<AgentInstallation>().WithMany().HasForeignKey(a => a.InstallationId).OnDelete(DeleteBehavior.Restrict);
        update.HasOne<UserAccount>().WithMany().HasForeignKey(a => a.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
