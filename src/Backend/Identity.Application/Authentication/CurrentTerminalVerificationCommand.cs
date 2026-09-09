using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Authentication;

// The bearer is supplied by the trusted PTS backend, never persisted or included in audit payloads.
public sealed record CurrentTerminalVerificationCommand(
    long TerminalId,
    string AccessToken,
    string ClientId,
    string ClientSecret,
    Guid CorrelationId,
    string? NewPin = null) : IRequest<TerminalVerificationResult>;

public sealed class CurrentTerminalVerificationHandler(
    IAuthenticationStore store,
    IAdministrationStore administrationStore,
    ISecretHasher secretHasher,
    ITerminalAccessTokenValidator tokens,
    IUnitOfWork unitOfWork,
    ITransactionRunner transactions,
    TimeProvider timeProvider,
    IPinHasher pinHasher)
    : IRequestHandler<CurrentTerminalVerificationCommand, TerminalVerificationResult>
{
    public ValueTask<TerminalVerificationResult> Handle(
        CurrentTerminalVerificationCommand request,
        CancellationToken cancellationToken) => new(transactions.Execute(
            ct => Verify(request, ct), cancellationToken));

    private async Task<TerminalVerificationResult> Verify(
        CurrentTerminalVerificationCommand request,
        CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var client = await store.FindClientByClientId(request.ClientId, ct);
        var app = client is null ? null : await store.FindApplication(client.ApplicationId, ct);
        async Task<TerminalVerificationResult> Reject(TerminalVerificationFailureCode reason, long? userId = null)
        {
            store.Add(AuthenticationAudit.CreateAuthenticationEvent(
                AuthenticationAuditEventType.TerminalVerificationFailed, false, reason.ToString(),
                request.CorrelationId, now, userId, app?.ApplicationId, client?.ApplicationClientId));
            await unitOfWork.SaveChangesAsync(ct);
            return TerminalVerificationResult.Rejected(reason);
        }

        if (client is null || app is null || !app.IsActive
            || client.ClientType != ApplicationClientType.Service || !client.IsActive
            || client.RevokedAt is not null || client.ExpiresAt <= now
            || client.ClientSecretHash is null || string.IsNullOrEmpty(request.ClientSecret)
            || !secretHasher.Verify(request.ClientSecret, client.ClientSecretHash))
            return await Reject(TerminalVerificationFailureCode.InvalidClient);

        // Validate the actual signed user token, not caller-supplied employee IDs or decoded claims.
        var identity = tokens.Validate(request.AccessToken, app.TokenAudience, now);
        if (identity is null || identity.ApplicationId != app.ApplicationId)
            return await Reject(TerminalVerificationFailureCode.InvalidCredentials);
        var browser = await store.FindClientByClientId(identity.ClientId, ct);
        if (browser is null || browser.ApplicationId != app.ApplicationId
            || browser.ClientType != ApplicationClientType.Public || !browser.IsActive
            || browser.RevokedAt is not null || browser.ExpiresAt <= now)
            return await Reject(TerminalVerificationFailureCode.InvalidClient);

        var device = await store.FindDevice(request.TerminalId, ct);
        if (device is null || !device.IsTrustedAt(now)
            || !string.Equals(device.DeviceType, "Terminal", StringComparison.OrdinalIgnoreCase))
            return await Reject(TerminalVerificationFailureCode.TerminalNotTrusted);
        var user = await store.FindUser(identity.UserId, ct);
        if (user is null || !user.IsActive || user.SecurityVersion != identity.SecurityVersion)
            return await Reject(TerminalVerificationFailureCode.InvalidCredentials);
        if (user.LockoutEndAt > now)
            return await Reject(TerminalVerificationFailureCode.LockedOut, user.UserId);
        var access = await store.FindUserApplication(user.UserId, app.ApplicationId, ct);
        var permissions = await administrationStore.GetEffectiveAuthorization(user.UserId, app.ApplicationId, now, ct);
        if (access is null || !access.IsActive || permissions is null
            || permissions.AuthorizationVersion != identity.AuthorizationVersion
            || !permissions.CapabilityCodes.Contains("pts.shopfloor.operate", StringComparer.Ordinal)
            || !permissions.CapabilityCodes.Contains("pts.production.read", StringComparer.Ordinal))
            return await Reject(TerminalVerificationFailureCode.AccessDenied, user.UserId);

        var pin = await store.FindCurrentPin(user.UserId, ct);
        if (request.NewPin is { } newPin)
        {
            // IAM password/MFA already proved ownership. This is initial-PIN activation,
            // not a general reset endpoint; it cannot replace an established user PIN.
            if (newPin.Length != 4 || newPin.Any(c => c is < '0' or > '9')
                || newPin == InitialEmployeePin.FromEmployeeCode(user.EmployeeCode)
                || pin is null || pin.RevokedAt is not null)
                return await Reject(TerminalVerificationFailureCode.InvalidNewPin, user.UserId);
            if (pin.RequiresChange)
            {
                if (pinHasher.Verify(newPin, pin))
                    return await Reject(TerminalVerificationFailureCode.InvalidNewPin, user.UserId);
                var hash = pinHasher.Hash(newPin);
                pin.Revoke(now);
                // Release the unique active-credential key inside the same transaction.
                await unitOfWork.SaveChangesAsync(ct);
                store.Add(UserCredential.CreatePin(user.UserId, hash.Algorithm, hash.IterationCount,
                    hash.Salt, hash.Hash, now, user.UserId));
                store.Add(AuthenticationAudit.CreateAuthenticationEvent(
                    AuthenticationAuditEventType.CredentialChanged, true, null,
                    request.CorrelationId, now, user.UserId, app.ApplicationId, browser.ApplicationClientId));
                // Keep the password/MFA-authenticated session; the old PIN is revoked immediately.
            }
            else if (!pinHasher.Verify(newPin, pin))
                return await Reject(TerminalVerificationFailureCode.InvalidNewPin, user.UserId);
            // Matching the already-established PIN allows retry after a lost success response.
        }
        else if (pin?.RequiresChange == true)
            return await Reject(TerminalVerificationFailureCode.PinChangeRequired, user.UserId);

        store.Add(AuthenticationAudit.CreateAuthenticationEvent(
            AuthenticationAuditEventType.TerminalVerificationSucceeded, true, null,
            request.CorrelationId, now, user.UserId, app.ApplicationId, client.ApplicationClientId,
            deviceId: request.TerminalId));
        await unitOfWork.SaveChangesAsync(ct);
        return TerminalVerificationResult.Verified(user.UserId, user.EmployeeCode, user.DisplayName, permissions.CapabilityCodes);
    }
}
