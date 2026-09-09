using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;
using Identity.Application.Persistence;
using Identity.Application.Security;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Mfa;

public sealed class MfaCommandHandler(
    IMfaStore store,
    IAuthenticationStore authenticationStore,
    IAdministrationStore administrationStore,
    IAdministrationAuthorizer authorizer,
    ISecretProtector secretProtector,
    ITotpService totpService,
    IChallengeHasher challengeHasher,
    IAuditPayloadSerializer auditPayloadSerializer,
    ISessionIssuer sessionIssuer,
    IUnitOfWork unitOfWork,
    ITransactionRunner transactionRunner,
    AuthenticationSecurityPolicy securityPolicy,
    TimeProvider timeProvider) :
    IRequestHandler<EnrollTotpCommand, TotpEnrollmentResult>,
    IRequestHandler<VerifyTotpEnrollmentCommand, OperationResult>,
    IRequestHandler<RevokeMfaMethodCommand, OperationResult>,
    IRequestHandler<CompleteMfaLoginCommand, AuthenticationResult>,
    IRequestHandler<TrustDeviceCommand, OperationResult>
{
    public ValueTask<TotpEnrollmentResult> Handle(
        EnrollTotpCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(async token =>
    {
        await authorizer.Authorize(request.Context, AdministrationAction.EnrollMfa, token);
        var user = await authenticationStore.FindUser(request.UserId, token)
            ?? throw new AdministrationException("User was not found.");
        if (!user.IsActive)
        {
            throw new AdministrationException("User is inactive.");
        }

        var now = UtcNow();
        var secret = totpService.GenerateSecret();
        var protectedSecret = secretProtector.Protect(secret);
        var method = UserMfaMethod.EnrollTotp(
            user.UserId,
            request.MethodName,
            protectedSecret.Ciphertext,
            protectedSecret.KeyId,
            request.IsPrimary,
            now);
        store.Add(method);
        await unitOfWork.SaveChangesAsync(token);
        AddAudit(
            AuthenticationAuditEventType.MfaMethodAdded,
            true,
            null,
            request.Context.CorrelationId,
            now,
            user.UserId,
            eventDataJson: SerializeMethod(method, "Added"));
        await unitOfWork.SaveChangesAsync(token);
        return new TotpEnrollmentResult(
            method.UserMfaMethodId,
            totpService.EncodeSecret(secret),
            "SHA1",
            6,
            30);
    }, cancellationToken));

    public ValueTask<OperationResult> Handle(
        VerifyTotpEnrollmentCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(async token =>
    {
        await authorizer.Authorize(request.Context, AdministrationAction.VerifyMfa, token);
        var method = await store.FindMethod(request.UserMfaMethodId, token)
            ?? throw new AdministrationException("MFA method was not found.");
        var now = UtcNow();
        var secret = GetTotpSecret(method);
        if (!totpService.Verify(secret, request.Code, now))
        {
            AddAudit(
                AuthenticationAuditEventType.MfaMethodVerificationRejected,
                false,
                AuthenticationFailureCode.MfaInvalid.ToString(),
                request.Context.CorrelationId,
                now,
                method.UserId,
                eventDataJson: SerializeMethod(method, "VerificationRejected"));
            await unitOfWork.SaveChangesAsync(token);
            return OperationResult.Rejected(AuthenticationFailureCode.MfaInvalid);
        }

        method.Verify(now);
        AddAudit(
            AuthenticationAuditEventType.MfaMethodVerified,
            true,
            null,
            request.Context.CorrelationId,
            now,
            method.UserId,
            eventDataJson: SerializeMethod(method, "Verified"));
        await unitOfWork.SaveChangesAsync(token);
        return OperationResult.Success;
    }, cancellationToken));

    public ValueTask<OperationResult> Handle(
        RevokeMfaMethodCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(async token =>
    {
        await authorizer.Authorize(request.Context, AdministrationAction.RevokeMfa, token);
        var method = await store.FindMethod(request.UserMfaMethodId, token)
            ?? throw new AdministrationException("MFA method was not found.");
        var now = UtcNow();
        method.Revoke(now);
        AddAudit(
            AuthenticationAuditEventType.MfaMethodRevoked,
            true,
            null,
            request.Context.CorrelationId,
            now,
            method.UserId,
            eventDataJson: SerializeMethod(method, "Revoked"));
        await unitOfWork.SaveChangesAsync(token);
        return OperationResult.Success;
    }, cancellationToken));

    public ValueTask<AuthenticationResult> Handle(
        CompleteMfaLoginCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(
            token => CompleteLogin(request, token),
            cancellationToken));

    public ValueTask<OperationResult> Handle(
        TrustDeviceCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(async token =>
    {
        await authorizer.Authorize(request.Context, AdministrationAction.TrustDevice, token);
        var device = await authenticationStore.FindDevice(request.DeviceId, token)
            ?? throw new AdministrationException("Device was not found.");
        var now = UtcNow();
        device.Trust(now, securityPolicy.DeviceTrustDuration);
        AddAudit(
            AuthenticationAuditEventType.DeviceTrusted,
            true,
            null,
            request.Context.CorrelationId,
            now,
            device.UserId,
            deviceId: device.DeviceId,
            eventDataJson: auditPayloadSerializer.Serialize(
                new DeviceAuditData(device.DeviceId, "Trusted")));
        await unitOfWork.SaveChangesAsync(token);
        return OperationResult.Success;
    }, cancellationToken));

    private async Task<AuthenticationResult> CompleteLogin(
        CompleteMfaLoginCommand request,
        CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var challenge = await store.FindChallenge(request.MfaChallengeId, cancellationToken);
        if (challenge is null)
        {
            AddAudit(
                AuthenticationAuditEventType.MfaChallengeRejected,
                false,
                AuthenticationFailureCode.MfaInvalid.ToString(),
                request.CorrelationId,
                now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthenticationResult.Rejected(AuthenticationFailureCode.MfaInvalid);
        }

        if (!challengeHasher.Verify(
            challenge.MfaChallengeId,
            challenge.ChallengeHash,
            challenge.ChallengeKeyId))
        {
            return await RejectChallenge(
                challenge,
                AuthenticationFailureCode.MfaInvalid,
                request.CorrelationId,
                now,
                cancellationToken);
        }

        if (challenge.VerifiedAt is not null || now > challenge.ExpiresAt)
        {
            return await RejectChallenge(
                challenge,
                AuthenticationFailureCode.MfaExpired,
                request.CorrelationId,
                now,
                cancellationToken);
        }

        var user = await authenticationStore.FindUser(challenge.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return await RejectChallenge(
                challenge,
                AuthenticationFailureCode.AccessDenied,
                request.CorrelationId,
                now,
                cancellationToken);
        }
        if (user.LockoutEndAt > now)
        {
            return await RejectChallenge(
                challenge,
                AuthenticationFailureCode.AccountLocked,
                request.CorrelationId,
                now,
                cancellationToken);
        }

        if (challenge.AttemptCount >= challenge.MaximumAttemptCount)
        {
            return await RejectChallenge(
                challenge,
                AuthenticationFailureCode.MfaAttemptsExceeded,
                request.CorrelationId,
                now,
                cancellationToken);
        }

        var method = await store.FindMethod(challenge.UserMfaMethodId, cancellationToken);
        if (method is null || !method.IsEnabled || !method.IsVerified || method.RevokedAt is not null)
        {
            return await RejectChallenge(
                challenge,
                AuthenticationFailureCode.MfaInvalid,
                request.CorrelationId,
                now,
                cancellationToken);
        }

        var verified = totpService.TryVerify(
            GetTotpSecret(method), request.Code, now, out var matchedTimeStep);
        if (!verified || !method.TryRecordUse(now, matchedTimeStep))
        {
            challenge.RecordFailedAttempt(now);
            user.RecordFailedVerification(
                now,
                securityPolicy.MaximumFailedAttempts,
                securityPolicy.LockoutDuration);
            var failureCode = user.LockoutEndAt > now
                ? AuthenticationFailureCode.AccountLocked
                : challenge.AttemptCount >= challenge.MaximumAttemptCount
                    ? AuthenticationFailureCode.MfaAttemptsExceeded
                    : AuthenticationFailureCode.MfaInvalid;
            return await RejectChallenge(
                challenge,
                failureCode,
                request.CorrelationId,
                now,
                cancellationToken);
        }

        var application = await authenticationStore.FindApplication(
            challenge.ApplicationId,
            cancellationToken);
        var client = await store.FindClient(challenge.ApplicationClientId, cancellationToken);
        var access = await authenticationStore.FindUserApplication(
            challenge.UserId,
            challenge.ApplicationId,
            cancellationToken);
        Device? device = null;
        if (challenge.DeviceId.HasValue)
        {
            device = await authenticationStore.FindDevice(challenge.DeviceId.Value, cancellationToken);
        }

        var authorization = await administrationStore.GetEffectiveAuthorization(
            challenge.UserId,
            challenge.ApplicationId,
            now,
            cancellationToken);
        if (application is null
            || !application.IsActive
            || client is null
            || !client.IsActive
            || client.RevokedAt is not null
            || client.ExpiresAt <= now
            || access is null
            || !access.IsActive
            || (challenge.DeviceId.HasValue
                && (device is not { IsActive: true, RevokedAt: null }
                    || device.UserId != challenge.UserId))
            || authorization is null)
        {
            return await RejectChallenge(
                challenge,
                AuthenticationFailureCode.AccessDenied,
                request.CorrelationId,
                now,
                cancellationToken);
        }

        challenge.Complete(now);
        user.RecordSuccessfulVerification(now);
        var session = await sessionIssuer.Issue(
            user,
            application,
            client,
            authorization,
            challenge.DeviceId,
            Guid.NewGuid(),
            now,
            cancellationToken);
        AddAudit(
            AuthenticationAuditEventType.MfaChallengeVerified,
            true,
            null,
            request.CorrelationId,
            now,
            user.UserId,
            application.ApplicationId,
            client.ApplicationClientId,
            challenge.DeviceId,
            eventDataJson: SerializeMethod(method, "ChallengeVerified"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session.Result;
    }

    private async Task<AuthenticationResult> RejectChallenge(
        MfaChallenge challenge,
        AuthenticationFailureCode failureCode,
        Guid correlationId,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        AddAudit(
            AuthenticationAuditEventType.MfaChallengeRejected,
            false,
            failureCode.ToString(),
            correlationId,
            occurredAt,
            challenge.UserId,
            challenge.ApplicationId,
            challenge.ApplicationClientId,
            challenge.DeviceId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AuthenticationResult.Rejected(failureCode);
    }

    private byte[] GetTotpSecret(UserMfaMethod method)
    {
        if (method.MethodType != MfaMethodType.Totp || method.SecretEncrypted is null)
        {
            throw new InvalidOperationException("TOTP method does not contain an encrypted secret.");
        }

        return secretProtector.Unprotect(method.SecretEncrypted, method.EncryptionKeyId);
    }

    private string SerializeMethod(UserMfaMethod method, string action) =>
        auditPayloadSerializer.Serialize(new MfaMethodAuditData(
            method.UserMfaMethodId,
            method.MethodType.ToString(),
            action));

    private void AddAudit(
        AuthenticationAuditEventType eventType,
        bool succeeded,
        string? failureCode,
        Guid correlationId,
        DateTime occurredAt,
        long? userId = null,
        long? applicationId = null,
        long? applicationClientId = null,
        long? deviceId = null,
        string? eventDataJson = null) => store.Add(AuthenticationAudit.CreateAuthenticationEvent(
            eventType,
            succeeded,
            failureCode,
            correlationId,
            occurredAt,
            userId,
            applicationId,
            applicationClientId,
            deviceId,
            eventDataJson: eventDataJson));

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
