using Identity.Domain.Entities;

namespace Identity.Application.Authentication;

public interface IAuthenticationStore
{
    Task<UserAccount?> FindUserByEmployeeCode(string employeeCode, CancellationToken cancellationToken);
    ValueTask<UserAccount?> FindUser(long userId, CancellationToken cancellationToken);
    Task<UserCredential?> FindCurrentPassword(long userId, CancellationToken cancellationToken);
    Task<UserCredential?> FindCurrentPin(long userId, CancellationToken cancellationToken);
    Task<ApplicationClient?> FindClientByClientId(string clientId, CancellationToken cancellationToken);
    ValueTask<RegisteredApplication?> FindApplication(long applicationId, CancellationToken cancellationToken);
    ValueTask<UserApplicationAccess?> FindUserApplication(
        long userId,
        long applicationId,
        CancellationToken cancellationToken);
    ValueTask<Device?> FindDevice(long deviceId, CancellationToken cancellationToken);
    Task<RefreshToken?> FindRefreshToken(byte[] tokenHash, CancellationToken cancellationToken);
    Task RevokeTokenFamily(
        long userId,
        long applicationId,
        Guid tokenFamilyId,
        DateTime revokedAt,
        CancellationToken cancellationToken);
    void Add<TEntity>(TEntity entity) where TEntity : class;
}
