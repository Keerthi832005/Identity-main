using System.Security.Cryptography;
using System.Text.Json;
using Identity.Application.Administration;
using Identity.Application.Agents;
using Identity.Application.Messaging;
using Identity.Application.Mfa;
using Identity.Application.Persistence;
using Identity.Application.Security;
using Identity.Contracts.Agents;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

internal sealed class AgentRegistry(IdentityDbContext db, IRequestDispatcher dispatcher,
    ITransactionRunner transactions, IIdentifierHasher hasher) : IAgentRegistry
{
    public Task<AgentEnrollmentResult> Enroll(AgentEnrollment request, AdministrationContext context, CancellationToken ct)
    {
        AgentProtocol.Text(request.Hostname, 200);
        AgentProtocol.PublicKey(request.PublicKey);
        if (request.InstallationId == Guid.Empty || context.ActorUserId is not > 0) throw new ArgumentException("Enrollment identity is required.");
        return transactions.Execute(async token =>
        {
            // Serializes retries for this installation, including the missing-key range.
            var existing = await db.Set<AgentInstallation>().FromSqlInterpolated(
                $"SELECT * FROM [Identity].[AgentInstallation] WITH (UPDLOCK,HOLDLOCK) WHERE InstallationId={request.InstallationId}")
                .SingleOrDefaultAsync(token);
            if (existing is not null)
            {
                if (existing.PublicKey != request.PublicKey) throw new ArgumentException("Installation is already bound to another key.");
                var device = await db.Devices.SingleAsync(d => d.DeviceId == existing.DeviceId, token);
                // Reinstallation must never re-trust a revoked or expired device.
                return new AgentEnrollmentResult(existing.InstallationId, existing.DeviceId, device.IsTrustedAt(DateTime.UtcNow));
            }
            var result = await dispatcher.Send(new RegisterDeviceCommand(context.ActorUserId.Value, request.Hostname,
                "Terminal", hasher.Hash(request.PublicKey), null, null, context), token);
            db.Set<AgentInstallation>().Add(AgentInstallation.Create(request.InstallationId, result.ResourceId,
                request.Hostname, request.PublicKey, DateTime.UtcNow));
            await db.SaveChangesAsync(token);
            await dispatcher.Send(new TrustDeviceCommand(result.ResourceId, context), token);
            return new AgentEnrollmentResult(request.InstallationId, result.ResourceId, true);
        }, ct);
    }

    public async Task<bool> Report(SignedMachineReport envelope, CancellationToken ct)
    {
        var installation = await db.Set<AgentInstallation>().SingleOrDefaultAsync(a => a.InstallationId == envelope.InstallationId, ct);
        if (installation is null) return false;
        var device = await db.Devices.AsNoTracking().SingleAsync(d => d.DeviceId == installation.DeviceId, ct);
        if (!device.IsActive || device.RevokedAt is not null) return false;
        MachineInventory report;
        try { report = AgentProtocol.Verify(envelope, installation.PublicKey, DateTimeOffset.UtcNow); }
        catch (Exception e) when (e is ArgumentException or FormatException or CryptographicException or JsonException) { return false; }
        if (installation.LastCapturedAt >= report.CapturedAt.UtcDateTime) return false;
        installation.Report(report.Hostname, report.AgentVersion, report.CapturedAt.UtcDateTime, DateTime.UtcNow,
            JsonSerializer.Serialize(report, AgentProtocol.Json));
        try
        {
            // The rowversion prevents concurrent older reports overwriting newer inventory.
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    private IQueryable<AgentSummary> Summaries(IQueryable<AgentInstallation> installations) => from a in installations
                                                                                               join d in db.Devices.AsNoTracking() on a.DeviceId equals d.DeviceId
                                                                                               select new AgentSummary(a.InstallationId, a.DeviceId, a.Hostname, a.AgentVersion, a.CreatedAt,
                                                                                                   a.LastReportAt, d.IsActive && d.RevokedAt == null, d.IsActive && d.RevokedAt == null && d.IsTrusted && d.TrustedUntil > DateTime.UtcNow, d.TrustedUntil,
                                                                                                   IdentityDbContext.JsonValue(a.InventoryJson, "$.windowsUsers.currentUserName"),
                                                                                                   IdentityDbContext.JsonValue(a.InventoryJson, "$.windowsUsers.sessionsReported") == "true");

    public async Task<AgentPage> Search(string? search, int skip, int take, CancellationToken ct)
    {
        var query = db.Set<AgentInstallation>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(a => a.Hostname.Contains(search));
        var total = await query.CountAsync(ct);
        var items = await Summaries(query.OrderBy(a => a.Hostname).ThenBy(a => a.InstallationId).Skip(skip).Take(take)).ToListAsync(ct);
        return new(items.Select(Utc).ToList(), total, skip, take);
    }
    public async Task<AgentDetails?> Get(Guid id, CancellationToken ct)
    {
        var summary = await Summaries(db.Set<AgentInstallation>().AsNoTracking().Where(a => a.InstallationId == id)).SingleOrDefaultAsync(ct);
        if (summary is null) return null;
        var json = await db.Set<AgentInstallation>().Where(a => a.InstallationId == id).Select(a => a.InventoryJson).SingleAsync(ct);
        return new(Utc(summary), json is null ? null : JsonSerializer.Deserialize<MachineInventory>(json, AgentProtocol.Json));
    }
    private static AgentSummary Utc(AgentSummary value) => value with
    {
        CreatedAt = DateTime.SpecifyKind(value.CreatedAt, DateTimeKind.Utc),
        LastReportAt = value.LastReportAt is { } seen ? DateTime.SpecifyKind(seen, DateTimeKind.Utc) : null,
        TrustedUntil = value.TrustedUntil is { } trust ? DateTime.SpecifyKind(trust, DateTimeKind.Utc) : null,
    };
}
