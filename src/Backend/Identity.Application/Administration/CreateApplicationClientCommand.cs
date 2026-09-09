using Identity.Application.Messaging;
using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed record CreateApplicationClientCommand(
    long ApplicationId,
    string ClientId,
    string ClientName,
    ApplicationClientType ClientType,
    byte[]? ClientSecretHash,
    DateTime? ExpiresAt,
    AdministrationContext Context) : IRequest<AdministrationResult>;
