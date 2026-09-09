using Identity.Application.Messaging;
using Identity.Application.Security;
using Identity.Domain.Enums;

namespace Identity.Application.Authentication;

public sealed record TerminalUserRole(string RoleCode, string RoleName);

public sealed record TerminalDirectoryUser(
    string EmployeeCode,
    string DisplayName,
    IReadOnlyList<TerminalUserRole> Roles);

public sealed record TerminalUserDirectoryPage(
    int Skip,
    int Take,
    int Total,
    IReadOnlyList<TerminalDirectoryUser> Items);

public interface ITerminalUserDirectory
{
    Task<TerminalUserDirectoryPage> List(
        long applicationId,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken);
}

public sealed record GetTerminalUserDirectoryQuery(
    long TerminalId,
    string ClientId,
    string ClientSecret,
    string? Search,
    int Skip,
    int Take) : IRequest<TerminalUserDirectoryResult>;

public sealed record TerminalUserDirectoryResult(
    bool Succeeded,
    TerminalVerificationFailureCode? FailureCode,
    TerminalUserDirectoryPage? Page)
{
    public static TerminalUserDirectoryResult Rejected(TerminalVerificationFailureCode failureCode) =>
        new(false, failureCode, null);

    public static TerminalUserDirectoryResult Listed(TerminalUserDirectoryPage page) =>
        new(true, null, page);
}

public sealed class TerminalUserDirectoryHandler(
    IAuthenticationStore store,
    ISecretHasher secretHasher,
    ITerminalUserDirectory directory,
    TimeProvider timeProvider)
    : IRequestHandler<GetTerminalUserDirectoryQuery, TerminalUserDirectoryResult>
{
    public async ValueTask<TerminalUserDirectoryResult> Handle(
        GetTerminalUserDirectoryQuery request,
        CancellationToken cancellationToken)
    {
        if (request.TerminalId <= 0 || string.IsNullOrWhiteSpace(request.ClientId)
            || request.ClientId.Length > 150 || string.IsNullOrWhiteSpace(request.ClientSecret)
            || request.ClientSecret.Length > 4096 || request.Skip < 0 || request.Take is < 1 or > 200
            || request.Search?.Length > 100)
            throw new ArgumentException("Invalid terminal user directory request.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var client = await store.FindClientByClientId(request.ClientId.Trim(), cancellationToken);
        var application = client is null
            ? null
            : await store.FindApplication(client.ApplicationId, cancellationToken);
        if (client?.ClientType != ApplicationClientType.Service
            || application is null
            || !application.IsActive
            || !string.Equals(application.ApplicationCode, "production-tracking", StringComparison.Ordinal)
            || !client.IsActive
            || client.RevokedAt is not null
            || client.ExpiresAt <= now
            || client.ClientSecretHash is null
            || !secretHasher.Verify(request.ClientSecret, client.ClientSecretHash))
            return TerminalUserDirectoryResult.Rejected(TerminalVerificationFailureCode.InvalidClient);

        var terminal = await store.FindDevice(request.TerminalId, cancellationToken);
        if (terminal is null || !terminal.IsTrustedAt(now)
            || !string.Equals(terminal.DeviceType, "Terminal", StringComparison.OrdinalIgnoreCase))
            return TerminalUserDirectoryResult.Rejected(TerminalVerificationFailureCode.TerminalNotTrusted);

        var page = await directory.List(
            application.ApplicationId,
            request.Search?.Trim(),
            request.Skip,
            request.Take,
            cancellationToken);
        return TerminalUserDirectoryResult.Listed(page);
    }
}
