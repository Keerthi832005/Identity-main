using System.Security.Cryptography;
using System.Text.Json;
using Identity.Application.Administration;
using Identity.Application.Agents;
using Identity.Application.Persistence;
using Identity.Contracts.Agents;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

internal sealed class AgentControlRegistry(IdentityDbContext db, ITransactionRunner transactions) : IAgentControlRegistry
{
    public async Task<AgentControlStatus?> Get(Guid id, CancellationToken ct)
    {
        if (!await db.Set<AgentInstallation>().AnyAsync(a => a.InstallationId == id, ct)) return null;
        var state = await db.Set<AgentControlState>().AsNoTracking().SingleOrDefaultAsync(a => a.InstallationId == id, ct);
        var update = await Latest(id, UpdateKind).AsNoTracking().FirstOrDefaultAsync(ct);
        var collect = await Latest(id, CollectKind).AsNoTracking().FirstOrDefaultAsync(ct);
        return new(state is null ? null : Utc(state.LastSeenAt), state?.AgentVersion, state?.SupervisorVersion,
            update is null ? null : Status(update), collect is null ? null : Status(collect));
    }

    public Task<AgentUpdateStatus?> RequestUpdate(Guid id, AdministrationContext actor, CancellationToken ct) =>
        Request(id, actor, UpdateKind, MinimumSupervisor, TimeSpan.FromMinutes(15), ct);

    // A collection is answered within one control poll, so it expires far sooner than an update download.
    public Task<AgentUpdateStatus?> RequestCollect(Guid id, AdministrationContext actor, CancellationToken ct) =>
        Request(id, actor, CollectKind, CollectAgent, TimeSpan.FromMinutes(5), ct);

    private Task<AgentUpdateStatus?> Request(Guid id, AdministrationContext actor, string kind, Version minimum,
        TimeSpan lifetime, CancellationToken ct)
    {
        if (actor.ActorUserId is not > 0) throw new ArgumentException("An administrator is required.");
        return transactions.Execute<AgentUpdateStatus?>(async token =>
        {
            var installation = await Lock(id, token);
            if (installation is null || !await Active(installation.DeviceId, token)) return null;
            // Each command is answered by the component that polls for it, so each gate reads that channel.
            var reported = kind == CollectKind
                ? (await db.Set<AgentCollectState>().SingleOrDefaultAsync(a => a.InstallationId == id, token))?.AgentVersion
                : (await db.Set<AgentControlState>().SingleOrDefaultAsync(a => a.InstallationId == id, token))?.SupervisorVersion;
            if (reported is null || !Version.TryParse(reported, out var version) || version < minimum) return null;
            var now = DateTime.UtcNow;
            var last = await Latest(id, kind).FirstOrDefaultAsync(token);
            if (last is not null && ((last.CompletedAt is null && last.ExpiresAt > now) || last.RequestedAt > now.AddMinutes(-1)))
                return Status(last);
            var request = new AgentUpdateRequest
            {
                Kind = kind,
                RequestId = Guid.NewGuid(),
                InstallationId = id,
                RequestedByUserId = actor.ActorUserId.Value,
                CorrelationId = actor.CorrelationId,
                RequestedAt = now,
                ExpiresAt = now.Add(lifetime)
            };
            db.Set<AgentUpdateRequest>().Add(request); await db.SaveChangesAsync(token);
            return Status(request);
        }, ct);
    }

    public Task<AgentControlReply?> Poll(SignedMachineReport proof, CancellationToken ct) => transactions.Execute<AgentControlReply?>(async token =>
    {
        // Locking the enrolled installation serializes first heartbeat, command requests and replies across IIS processes.
        var installation = await Lock(proof.InstallationId, token);
        if (installation is null || !await Active(installation.DeviceId, token)) return null;
        AgentControlReport report;
        var now = DateTimeOffset.UtcNow;
        try { report = AgentControlProtocol.Verify(proof, installation.PublicKey, now); }
        catch (Exception e) when (e is ArgumentException or FormatException or CryptographicException or JsonException) { return null; }
        var state = await db.Set<AgentControlState>().SingleOrDefaultAsync(a => a.InstallationId == proof.InstallationId, token);
        if (state is not null && state.LastSignedAt >= report.SignedAt.UtcDateTime) return null;
        if (state is null) { state = new() { InstallationId = proof.InstallationId }; db.Set<AgentControlState>().Add(state); }
        state.LastSignedAt = report.SignedAt.UtcDateTime; state.LastSeenAt = now.UtcDateTime;
        state.AgentVersion = report.AgentVersion; state.SupervisorVersion = report.SupervisorVersion;
        if (report.Acknowledgement is { } ack)
        {
            var acknowledged = await db.Set<AgentUpdateRequest>().SingleOrDefaultAsync(a => a.RequestId == ack.RequestId
                && a.InstallationId == proof.InstallationId && a.Kind == UpdateKind, token);
            if (acknowledged is { DeliveredAt: not null, CompletedAt: null } && acknowledged.RequestedAt > now.UtcDateTime.AddHours(-1))
            { acknowledged.CompletedAt = now.UtcDateTime; acknowledged.Result = ack.Result; acknowledged.AgentVersion = report.AgentVersion; }
        }
        var pending = await Deliver(proof.InstallationId, UpdateKind, now, token);
        await db.SaveChangesAsync(token);
        return new(pending is null ? null : new(pending.RequestId, Utc(pending.ExpiresAt)));
    }, ct);

    public Task<AgentCollectReply?> PollCollect(SignedMachineReport proof, CancellationToken ct) => transactions.Execute<AgentCollectReply?>(async token =>
    {
        var installation = await Lock(proof.InstallationId, token);
        if (installation is null || !await Active(installation.DeviceId, token)) return null;
        AgentCollectReport report;
        var now = DateTimeOffset.UtcNow;
        try { report = AgentCollectProtocol.Verify(proof, installation.PublicKey, now); }
        catch (Exception e) when (e is ArgumentException or FormatException or CryptographicException or JsonException) { return null; }
        var state = await db.Set<AgentCollectState>().SingleOrDefaultAsync(a => a.InstallationId == proof.InstallationId, token);
        if (state is not null && state.LastSignedAt >= report.SignedAt.UtcDateTime) return null;
        if (state is null) { state = new() { InstallationId = proof.InstallationId }; db.Set<AgentCollectState>().Add(state); }
        state.LastSignedAt = report.SignedAt.UtcDateTime; state.LastSeenAt = now.UtcDateTime; state.AgentVersion = report.AgentVersion;
        if (report.Acknowledgement is { } ack)
        {
            var acknowledged = await db.Set<AgentUpdateRequest>().SingleOrDefaultAsync(a => a.RequestId == ack.RequestId
                && a.InstallationId == proof.InstallationId && a.Kind == CollectKind, token);
            if (acknowledged is { DeliveredAt: not null, CompletedAt: null } && acknowledged.RequestedAt > now.UtcDateTime.AddHours(-1))
            { acknowledged.CompletedAt = now.UtcDateTime; acknowledged.Result = ack.Result; acknowledged.AgentVersion = report.AgentVersion; }
        }
        var pending = await Deliver(proof.InstallationId, CollectKind, now, token);
        await db.SaveChangesAsync(token);
        return new(pending is null ? null : new AgentCollectCommand(pending.RequestId, Utc(pending.ExpiresAt)));
    }, ct);

    private async Task<AgentUpdateRequest?> Deliver(Guid id, string kind, DateTimeOffset now, CancellationToken ct)
    {
        var pending = await db.Set<AgentUpdateRequest>().Where(a => a.InstallationId == id && a.Kind == kind
            && a.CompletedAt == null && a.ExpiresAt > now.UtcDateTime).OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync(ct);
        // A tracked acknowledgement may still appear in the database query until SaveChanges.
        if (pending?.CompletedAt is not null) return null;
        if (pending is not null) pending.DeliveredAt ??= now.UtcDateTime;
        return pending;
    }

    private Task<AgentInstallation?> Lock(Guid id, CancellationToken ct) => db.Set<AgentInstallation>().FromSqlInterpolated(
        $"SELECT * FROM [Identity].[AgentInstallation] WITH (UPDLOCK,HOLDLOCK) WHERE InstallationId={id}").SingleOrDefaultAsync(ct);
    private Task<bool> Active(long deviceId, CancellationToken ct) => db.Devices.AnyAsync(d => d.DeviceId == deviceId && d.IsActive && d.RevokedAt == null, ct);
    private const string UpdateKind = "update";
    private const string CollectKind = "collect";
    private static readonly Version MinimumSupervisor = new(1, 0, 1);
    private static readonly Version CollectAgent = new(1, 0, 6);
    private IOrderedQueryable<AgentUpdateRequest> Latest(Guid id, string kind) => db.Set<AgentUpdateRequest>()
        .Where(a => a.InstallationId == id && a.Kind == kind)
        .OrderByDescending(a => a.RequestedAt).ThenByDescending(a => a.RequestId);
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static AgentUpdateStatus Status(AgentUpdateRequest request) => new(request.RequestId, Utc(request.RequestedAt), Utc(request.ExpiresAt),
        request.DeliveredAt is { } delivered ? Utc(delivered) : null, request.CompletedAt is { } completed ? Utc(completed) : null,
        request.Result ?? (request.ExpiresAt <= DateTime.UtcNow ? "expired" : request.DeliveredAt is null ? "queued" : "received"), request.AgentVersion);
}
