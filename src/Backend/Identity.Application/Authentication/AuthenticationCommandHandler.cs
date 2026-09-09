using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Application.Mfa;
using Identity.Application.Persistence;
using Identity.Application.Security;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Authentication;

public sealed class AuthenticationCommandHandler(
    IAuthenticationStore store,
    IAdministrationStore administrationStore,
    IAdministrationAuthorizer administrationAuthorizer,
    IPasswordHasher passwordHasher,
    IPinHasher pinHasher,
    ISecretHasher secretHasher,
    IAccessTokenIssuer accessTokenIssuer,
    ISessionIssuer sessionIssuer,
    IMfaStore mfaStore,
    IChallengeHasher challengeHasher,
    IIdentifierHasher identifierHasher,
    IUnitOfWork unitOfWork,
    ITransactionRunner transactionRunner,
    AuthenticationSecurityPolicy securityPolicy,
    TimeProvider timeProvider) :
    IRequestHandler<SetPasswordCommand, OperationResult>,
    IRequestHandler<ChangeOwnPasswordCommand, OperationResult>,
    IRequestHandler<SetPinCommand, OperationResult>,
    IRequestHandler<TerminalVerificationCommand, TerminalVerificationResult>,
    IRequestHandler<LoginCommand, AuthenticationResult>,
    IRequestHandler<RefreshSessionCommand, AuthenticationResult>,
    IRequestHandler<LogoutCommand, OperationResult>,
    IRequestHandler<GetSigningMetadataQuery, SigningMetadata>
{
    public ValueTask<OperationResult> Handle(
        ChangeOwnPasswordCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(
            token => ChangeOwnPassword(request, token),
            cancellationToken));

    public ValueTask<OperationResult> Handle(
        SetPasswordCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(async token =>
    {
        await administrationAuthorizer.Authorize(
            request.Context,
            AdministrationAction.SetPassword,
            token);
        var user = await store.FindUser(request.UserId, token)
            ?? throw new AdministrationException("User was not found.");
        var now = UtcNow();
        if (request.ExpiresAt.HasValue && request.ExpiresAt <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Password expiry must be in the future.");
        }

        var password = passwordHasher.Hash(request.Password);
        var current = await store.FindCurrentPassword(user.UserId, token);
        current?.Revoke(now);
        user.InvalidateSecurity(now);
        store.Add(UserCredential.CreatePassword(
            user.UserId,
            password.Algorithm,
            password.IterationCount,
            password.Salt,
            password.Hash,
            now,
            request.ExpiresAt,
            request.Context.ActorUserId));
        AddAudit(
            AuthenticationAuditEventType.CredentialChanged,
            true,
            null,
            request.Context.CorrelationId,
            now,
            user.UserId);
        await unitOfWork.SaveChangesAsync(token);
        return OperationResult.Success;
    }, cancellationToken));

    public ValueTask<OperationResult> Handle(
        SetPinCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(async token =>
    {
        await administrationAuthorizer.Authorize(
            request.Context,
            AdministrationAction.SetPin,
            token);
        var user = await store.FindUser(request.UserId, token)
            ?? throw new AdministrationException("User was not found.");
        var now = UtcNow();
        var pin = pinHasher.Hash(request.Pin);
        var current = await store.FindCurrentPin(user.UserId, token);
        current?.Revoke(now);
        user.InvalidateSecurity(now);
        store.Add(UserCredential.CreatePin(
            user.UserId,
            pin.Algorithm,
            pin.IterationCount,
            pin.Salt,
            pin.Hash,
            now,
            request.Context.ActorUserId));
        AddAudit(
            AuthenticationAuditEventType.CredentialChanged,
            true,
            null,
            request.Context.CorrelationId,
            now,
            user.UserId);
        await unitOfWork.SaveChangesAsync(token);
        return OperationResult.Success;
    }, cancellationToken));

    public ValueTask<TerminalVerificationResult> Handle(
        TerminalVerificationCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(
            token => VerifyTerminal(request, token),
            cancellationToken));

    public ValueTask<AuthenticationResult> Handle(
        LoginCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(
            token => Login(request, token),
            cancellationToken));

    public ValueTask<AuthenticationResult> Handle(
        RefreshSessionCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(
            token => Refresh(request, token),
            cancellationToken));

    public ValueTask<OperationResult> Handle(
        LogoutCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(
            token => Logout(request, token),
            cancellationToken));

    public ValueTask<SigningMetadata> Handle(
        GetSigningMetadataQuery request,
        CancellationToken cancellationToken) => ValueTask.FromResult(accessTokenIssuer.GetSigningMetadata());

    private async Task<AuthenticationResult> Login(LoginCommand request, CancellationToken cancellationToken)
    {
        securityPolicy.Validate();
        var now = UtcNow();
        var loginIdentifierHash = identifierHasher.Hash(request.EmployeeCode);
        var client = await store.FindClientByClientId(request.ClientId, cancellationToken);
        var application = client is null
            ? null
            : await store.FindApplication(client.ApplicationId, cancellationToken);
        if (!IsValidClient(client, application, request.ClientSecret, now))
        {
            return await RejectAuthentication(
                AuthenticationAuditEventType.LoginFailed,
                AuthenticationFailureCode.InvalidClient,
                request.CorrelationId,
                now,
                cancellationToken,
                loginIdentifierHash: loginIdentifierHash);
        }

        var user = await store.FindUserByEmployeeCode(request.EmployeeCode, cancellationToken);
        if (user?.LockoutEndAt > now)
        {
            return await RejectAuthentication(
                AuthenticationAuditEventType.LoginFailed,
                AuthenticationFailureCode.AccountLocked,
                request.CorrelationId,
                now,
                cancellationToken,
                user.UserId,
                application!.ApplicationId,
                client!.ApplicationClientId,
                loginIdentifierHash: loginIdentifierHash);
        }

        var credential = user is null
            ? null
            : await store.FindCurrentPassword(user.UserId, cancellationToken);
        var passwordValid = passwordHasher.Verify(request.Password, credential);
        if (user is null
            || !user.IsActive
            || credential is null
            || credential.RevokedAt is not null
            || credential.ExpiresAt <= now
            || !passwordValid)
        {
            if (user is { IsActive: true })
            {
                user.RecordFailedVerification(
                    now,
                    securityPolicy.MaximumFailedAttempts,
                    securityPolicy.LockoutDuration);
            }
            return await RejectAuthentication(
                AuthenticationAuditEventType.LoginFailed,
                AuthenticationFailureCode.InvalidCredentials,
                request.CorrelationId,
                now,
                cancellationToken,
                user?.UserId,
                application!.ApplicationId,
                client!.ApplicationClientId,
                loginIdentifierHash: loginIdentifierHash);
        }

        var access = await store.FindUserApplication(
            user.UserId,
            application!.ApplicationId,
            cancellationToken);
        if (access is null || !access.IsActive)
        {
            return await RejectAuthentication(
                AuthenticationAuditEventType.LoginFailed,
                AuthenticationFailureCode.AccessDenied,
                request.CorrelationId,
                now,
                cancellationToken,
                user.UserId,
                application.ApplicationId,
                client!.ApplicationClientId,
                loginIdentifierHash: loginIdentifierHash);
        }

        Device? device = null;
        if (request.DeviceId.HasValue)
        {
            device = await store.FindDevice(request.DeviceId.Value, cancellationToken);
        }

        if (request.DeviceId.HasValue
            && (device is not { IsActive: true, RevokedAt: null } || device.UserId != user.UserId))
        {
            return await RejectAuthentication(
                AuthenticationAuditEventType.LoginFailed,
                AuthenticationFailureCode.AccessDenied,
                request.CorrelationId,
                now,
                cancellationToken,
                user.UserId,
                application.ApplicationId,
                client!.ApplicationClientId,
                request.DeviceId,
                loginIdentifierHash);
        }

        var authorization = await administrationStore.GetEffectiveAuthorization(
            user.UserId,
            application.ApplicationId,
            now,
            cancellationToken);
        if (authorization is null)
        {
            return await RejectAuthentication(
                AuthenticationAuditEventType.LoginFailed,
                AuthenticationFailureCode.AccessDenied,
                request.CorrelationId,
                now,
                cancellationToken,
                user.UserId,
                application.ApplicationId,
                client!.ApplicationClientId,
                request.DeviceId,
                loginIdentifierHash);
        }

        var primaryTotp = await mfaStore.FindPrimaryTotp(user.UserId, cancellationToken);
        if (primaryTotp is not null && device?.IsTrustedAt(now) != true)
        {
            var challengeId = Guid.NewGuid();
            var challenge = MfaChallenge.Create(
                challengeId,
                user.UserId,
                application.ApplicationId,
                client!.ApplicationClientId,
                primaryTotp.UserMfaMethodId,
                request.DeviceId,
                challengeHasher.Hash(challengeId),
                challengeHasher.KeyId,
                now,
                now.AddMinutes(5),
                5,
                request.CorrelationId);
            mfaStore.Add(challenge);
            AddAudit(
                AuthenticationAuditEventType.MfaChallengeCreated,
                true,
                null,
                request.CorrelationId,
                now,
                user.UserId,
                application.ApplicationId,
                client.ApplicationClientId,
                request.DeviceId,
                loginIdentifierHash);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthenticationResult.MfaRequired(challengeId);
        }

        user.RecordSuccessfulVerification(now);
        var session = await sessionIssuer.Issue(
            user,
            application,
            client!,
            authorization,
            request.DeviceId,
            Guid.NewGuid(),
            now,
            cancellationToken);
        AddAudit(
            AuthenticationAuditEventType.LoginSucceeded,
            true,
            null,
            request.CorrelationId,
            now,
            user.UserId,
            application.ApplicationId,
            client!.ApplicationClientId,
            request.DeviceId,
            loginIdentifierHash);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session.Result;
    }

    private async Task<TerminalVerificationResult> VerifyTerminal(
        TerminalVerificationCommand request,
        CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var identifierHash = identifierHasher.Hash(request.EmployeeCode);
        var client = await store.FindClientByClientId(request.ClientId, cancellationToken);
        var application = client is null
            ? null
            : await store.FindApplication(client.ApplicationId, cancellationToken);
        if (client?.ClientType != ApplicationClientType.Service
            || !IsValidClient(client, application, request.ClientSecret, now))
            return await RejectTerminal(
                TerminalVerificationFailureCode.InvalidClient,
                request.CorrelationId, now, cancellationToken,
                loginIdentifierHash: identifierHash);

        var terminal = await store.FindDevice(request.TerminalId, cancellationToken);
        if (terminal is null || !terminal.IsTrustedAt(now)
            || !string.Equals(terminal.DeviceType, "Terminal", StringComparison.OrdinalIgnoreCase))
            return await RejectTerminal(
                TerminalVerificationFailureCode.TerminalNotTrusted,
                request.CorrelationId, now, cancellationToken,
                application!.ApplicationId, client.ApplicationClientId,
                loginIdentifierHash: identifierHash);

        var user = await store.FindUserByEmployeeCode(request.EmployeeCode, cancellationToken);
        var credential = user is null
            ? null
            : await store.FindCurrentPin(user.UserId, cancellationToken);
        var pinValid = pinHasher.Verify(request.Pin, credential);
        if (user?.LockoutEndAt > now)
            return await RejectTerminal(
                TerminalVerificationFailureCode.LockedOut,
                request.CorrelationId, now, cancellationToken,
                application!.ApplicationId, client.ApplicationClientId,
                user.UserId, identifierHash);

        if (user is null || !user.IsActive || credential is null
            || credential.RevokedAt is not null || !pinValid)
        {
            user?.RecordFailedVerification(
                now,
                securityPolicy.MaximumFailedAttempts,
                securityPolicy.LockoutDuration);
            return await RejectTerminal(
                TerminalVerificationFailureCode.InvalidCredentials,
                request.CorrelationId, now, cancellationToken,
                application!.ApplicationId, client.ApplicationClientId,
                user?.UserId, identifierHash);
        }

        var access = await store.FindUserApplication(
            user.UserId, application!.ApplicationId, cancellationToken);
        if (access is null || !access.IsActive)
            return await RejectTerminal(
                TerminalVerificationFailureCode.AccessDenied,
                request.CorrelationId, now, cancellationToken,
                application.ApplicationId, client.ApplicationClientId,
                user.UserId, identifierHash);

        if (credential.RequiresChange)
            return await RejectTerminal(
                TerminalVerificationFailureCode.PinChangeRequired,
                request.CorrelationId, now, cancellationToken,
                application.ApplicationId, client.ApplicationClientId,
                user.UserId, identifierHash);

        user.RecordSuccessfulVerification(now);
        AddAudit(
            AuthenticationAuditEventType.TerminalVerificationSucceeded,
            true,
            null,
            request.CorrelationId,
            now,
            user.UserId,
            application.ApplicationId,
            client.ApplicationClientId,
            loginIdentifierHash: identifierHash);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var authorization = await administrationStore.GetEffectiveAuthorization(
            user.UserId, application.ApplicationId, now, cancellationToken);
        return TerminalVerificationResult.Verified(
            user.UserId, user.EmployeeCode, user.DisplayName, authorization?.CapabilityCodes ?? []);
    }

    private async Task<TerminalVerificationResult> RejectTerminal(
        TerminalVerificationFailureCode failureCode,
        Guid correlationId,
        DateTime occurredAt,
        CancellationToken cancellationToken,
        long? applicationId = null,
        long? applicationClientId = null,
        long? userId = null,
        byte[]? loginIdentifierHash = null)
    {
        AddAudit(
            AuthenticationAuditEventType.TerminalVerificationFailed,
            false,
            failureCode.ToString(),
            correlationId,
            occurredAt,
            userId,
            applicationId,
            applicationClientId,
            loginIdentifierHash: loginIdentifierHash);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TerminalVerificationResult.Rejected(failureCode);
    }

    private async Task<AuthenticationResult> Refresh(
        RefreshSessionCommand request,
        CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var tokenHash = secretHasher.Hash(request.RefreshToken);
        var refreshToken = await store.FindRefreshToken(tokenHash, cancellationToken);
        if (refreshToken is null)
        {
            return await RejectAuthentication(
                AuthenticationAuditEventType.TokenRefreshRejected,
                AuthenticationFailureCode.InvalidRefreshToken,
                request.CorrelationId,
                now,
                cancellationToken);
        }

        var client = await store.FindClientByClientId(request.ClientId, cancellationToken);
        var application = await store.FindApplication(refreshToken.ApplicationId, cancellationToken);
        if (client is null
            || client.ApplicationClientId != refreshToken.ApplicationClientId
            || !IsValidClient(client, application, request.ClientSecret, now))
        {
            return await RejectRefresh(
                refreshToken,
                AuthenticationFailureCode.InvalidClient,
                request.CorrelationId,
                now,
                cancellationToken);
        }

        if (refreshToken.ConsumedAt is not null || refreshToken.RevokedAt is not null)
        {
            await store.RevokeTokenFamily(
                refreshToken.UserId,
                refreshToken.ApplicationId,
                refreshToken.TokenFamilyId,
                now,
                cancellationToken);
            return await RejectRefresh(
                refreshToken,
                AuthenticationFailureCode.RefreshTokenReused,
                request.CorrelationId,
                now,
                cancellationToken);
        }

        if (refreshToken.ExpiresAt <= now)
        {
            refreshToken.Revoke(now);
            return await RejectRefresh(
                refreshToken,
                AuthenticationFailureCode.RefreshTokenExpired,
                request.CorrelationId,
                now,
                cancellationToken);
        }

        var user = await store.FindUser(refreshToken.UserId, cancellationToken);
        var access = await store.FindUserApplication(
            refreshToken.UserId,
            refreshToken.ApplicationId,
            cancellationToken);
        var authorization = await administrationStore.GetEffectiveAuthorization(
            refreshToken.UserId,
            refreshToken.ApplicationId,
            now,
            cancellationToken);
        if (user is null
            || !user.IsActive
            || access is null
            || !access.IsActive
            || application is null
            || !application.IsActive
            || authorization is null
            || refreshToken.SecurityVersion != user.SecurityVersion
            || refreshToken.AuthorizationVersion != authorization.AuthorizationVersion)
        {
            await store.RevokeTokenFamily(
                refreshToken.UserId,
                refreshToken.ApplicationId,
                refreshToken.TokenFamilyId,
                now,
                cancellationToken);
            return await RejectRefresh(
                refreshToken,
                AuthenticationFailureCode.SessionVersionStale,
                request.CorrelationId,
                now,
                cancellationToken);
        }

        refreshToken.Consume(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var session = await sessionIssuer.Issue(
            user,
            application,
            client,
            authorization,
            refreshToken.DeviceId,
            refreshToken.TokenFamilyId,
            now,
            cancellationToken);
        refreshToken.SetReplacement(session.RefreshToken.RefreshTokenId);
        AddAudit(
            AuthenticationAuditEventType.TokenRefreshed,
            true,
            null,
            request.CorrelationId,
            now,
            user.UserId,
            application.ApplicationId,
            client.ApplicationClientId,
            refreshToken.DeviceId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session.Result;
    }

    private async Task<OperationResult> ChangeOwnPassword(
        ChangeOwnPasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            throw new ArgumentException("The new password must be different from the current password.");
        }

        var now = UtcNow();
        var refreshToken = await store.FindRefreshToken(
            secretHasher.Hash(request.RefreshToken),
            cancellationToken);
        var client = await store.FindClientByClientId(request.ClientId, cancellationToken);
        var application = refreshToken is null
            ? null
            : await store.FindApplication(refreshToken.ApplicationId, cancellationToken);

        if (refreshToken is null
            || refreshToken.ConsumedAt is not null
            || refreshToken.RevokedAt is not null
            || refreshToken.ExpiresAt <= now
            || client is null
            || client.ApplicationClientId != refreshToken.ApplicationClientId
            || !IsValidClient(client, application, null, now))
        {
            return OperationResult.Rejected(AuthenticationFailureCode.InvalidRefreshToken);
        }

        var user = await store.FindUser(refreshToken.UserId, cancellationToken);
        var current = user is null
            ? null
            : await store.FindCurrentPassword(user.UserId, cancellationToken);
        if (user is null
            || !user.IsActive
            || refreshToken.SecurityVersion != user.SecurityVersion
            || !passwordHasher.Verify(request.CurrentPassword, current))
        {
            AddAudit(
                AuthenticationAuditEventType.CredentialChanged,
                false,
                AuthenticationFailureCode.InvalidCredentials.ToString(),
                request.CorrelationId,
                now,
                refreshToken.UserId,
                refreshToken.ApplicationId,
                refreshToken.ApplicationClientId,
                refreshToken.DeviceId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return OperationResult.Rejected(AuthenticationFailureCode.InvalidCredentials);
        }

        var replacement = passwordHasher.Hash(request.NewPassword);
        current!.Revoke(now);
        user.InvalidateSecurity(now);
        store.Add(UserCredential.CreatePassword(
            user.UserId,
            replacement.Algorithm,
            replacement.IterationCount,
            replacement.Salt,
            replacement.Hash,
            now,
            null,
            user.UserId));
        await store.RevokeTokenFamily(
            refreshToken.UserId,
            refreshToken.ApplicationId,
            refreshToken.TokenFamilyId,
            now,
            cancellationToken);
        AddAudit(
            AuthenticationAuditEventType.CredentialChanged,
            true,
            null,
            request.CorrelationId,
            now,
            user.UserId,
            refreshToken.ApplicationId,
            refreshToken.ApplicationClientId,
            refreshToken.DeviceId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OperationResult.Success;
    }

    private async Task<OperationResult> Logout(LogoutCommand request, CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var refreshToken = await store.FindRefreshToken(
            secretHasher.Hash(request.RefreshToken),
            cancellationToken);
        if (refreshToken is null)
        {
            return OperationResult.Success;
        }

        await store.RevokeTokenFamily(
            refreshToken.UserId,
            refreshToken.ApplicationId,
            refreshToken.TokenFamilyId,
            now,
            cancellationToken);
        AddAudit(
            AuthenticationAuditEventType.LoggedOut,
            true,
            null,
            request.CorrelationId,
            now,
            refreshToken.UserId,
            refreshToken.ApplicationId,
            refreshToken.ApplicationClientId,
            refreshToken.DeviceId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OperationResult.Success;
    }

    private async Task<AuthenticationResult> RejectRefresh(
        RefreshToken refreshToken,
        AuthenticationFailureCode failureCode,
        Guid correlationId,
        DateTime occurredAt,
        CancellationToken cancellationToken) => await RejectAuthentication(
            AuthenticationAuditEventType.TokenRefreshRejected,
            failureCode,
            correlationId,
            occurredAt,
            cancellationToken,
            refreshToken.UserId,
            refreshToken.ApplicationId,
            refreshToken.ApplicationClientId,
            refreshToken.DeviceId);

    private async Task<AuthenticationResult> RejectAuthentication(
        AuthenticationAuditEventType eventType,
        AuthenticationFailureCode failureCode,
        Guid correlationId,
        DateTime occurredAt,
        CancellationToken cancellationToken,
        long? userId = null,
        long? applicationId = null,
        long? applicationClientId = null,
        long? deviceId = null,
        byte[]? loginIdentifierHash = null)
    {
        AddAudit(
            eventType,
            false,
            failureCode.ToString(),
            correlationId,
            occurredAt,
            userId,
            applicationId,
            applicationClientId,
            deviceId,
            loginIdentifierHash);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AuthenticationResult.Rejected(failureCode);
    }

    private bool IsValidClient(
        ApplicationClient? client,
        RegisteredApplication? application,
        string? suppliedSecret,
        DateTime now)
    {
        if (client is null
            || application is null
            || !client.IsActive
            || !application.IsActive
            || client.RevokedAt is not null
            || client.ExpiresAt <= now)
        {
            return false;
        }

        return client.ClientType == ApplicationClientType.Public
            ? string.IsNullOrEmpty(suppliedSecret)
            : !string.IsNullOrEmpty(suppliedSecret)
                && client.ClientSecretHash is not null
                && secretHasher.Verify(suppliedSecret, client.ClientSecretHash);
    }

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
        byte[]? loginIdentifierHash = null) => store.Add(AuthenticationAudit.CreateAuthenticationEvent(
            eventType,
            succeeded,
            failureCode,
            correlationId,
            occurredAt,
            userId,
            applicationId,
            applicationClientId,
            deviceId,
            loginIdentifierHash));

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
