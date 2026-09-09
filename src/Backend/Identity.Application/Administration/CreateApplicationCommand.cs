using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record CreateApplicationCommand(
    string ApplicationCode,
    string ApplicationName,
    string? Description,
    string TokenAudience,
    int AccessTokenLifetimeMinutes,
    int RefreshTokenLifetimeDays,
    AdministrationContext Context) : IRequest<AdministrationResult>;
