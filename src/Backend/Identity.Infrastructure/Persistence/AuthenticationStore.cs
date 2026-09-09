using Identity.Application.Authentication;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

internal sealed class AuthenticationStore(IdentityDbContext dbContext) : IAuthenticationStore
{
    public Task<UserAccount?> FindUserByEmployeeCode(
        string employeeCode,
        CancellationToken cancellationToken)
    {
        var normalized = employeeCode.Trim().ToUpperInvariant();
        return dbContext.UserAccounts.SingleOrDefaultAsync(
            user => EF.Property<string>(user, "NormalizedEmployeeCode") == normalized,
            cancellationToken);
    }

    public ValueTask<UserAccount?> FindUser(long userId, CancellationToken cancellationToken) =>
        dbContext.UserAccounts.FindAsync([userId], cancellationToken);

    public Task<UserCredential?> FindCurrentPassword(
        long userId,
        CancellationToken cancellationToken) => dbContext.UserCredentials.SingleOrDefaultAsync(
            credential => credential.UserId == userId
                && credential.CredentialType == CredentialType.Password
                && credential.RevokedAt == null,
            cancellationToken);

    public Task<UserCredential?> FindCurrentPin(
        long userId,
        CancellationToken cancellationToken) => dbContext.UserCredentials.SingleOrDefaultAsync(
            credential => credential.UserId == userId
                && credential.CredentialType == CredentialType.Pin
                && credential.RevokedAt == null,
            cancellationToken);

    public Task<ApplicationClient?> FindClientByClientId(
        string clientId,
        CancellationToken cancellationToken)
    {
        var normalized = clientId.Trim().ToUpperInvariant();
        return dbContext.ApplicationClients.SingleOrDefaultAsync(
            client => EF.Property<string>(client, "NormalizedClientId") == normalized,
            cancellationToken);
    }

    public ValueTask<RegisteredApplication?> FindApplication(
        long applicationId,
        CancellationToken cancellationToken) => dbContext.Applications.FindAsync(
            [applicationId],
            cancellationToken);

    public ValueTask<UserApplicationAccess?> FindUserApplication(
        long userId,
        long applicationId,
        CancellationToken cancellationToken) => dbContext.UserApplications.FindAsync(
            [userId, applicationId],
            cancellationToken);

    public ValueTask<Device?> FindDevice(long deviceId, CancellationToken cancellationToken) =>
        dbContext.Devices.FindAsync([deviceId], cancellationToken);

    public Task<RefreshToken?> FindRefreshToken(
        byte[] tokenHash,
        CancellationToken cancellationToken) => dbContext.RefreshTokens.SingleOrDefaultAsync(
            token => token.TokenHash == tokenHash,
            cancellationToken);

    public async Task RevokeTokenFamily(
        long userId,
        long applicationId,
        Guid tokenFamilyId,
        DateTime revokedAt,
        CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId
                && token.ApplicationId == applicationId
                && token.TokenFamilyId == tokenFamilyId
                && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(revokedAt);
        }
    }

    public void Add<TEntity>(TEntity entity) where TEntity : class => dbContext.Add(entity);
}
