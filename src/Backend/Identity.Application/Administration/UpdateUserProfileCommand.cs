using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record UpdateUserProfileCommand(
    long UserId,
    string DisplayName,
    string? Email,
    long? ManagerUserId,
    AdministrationContext Context,
    UserOrganizationMapping? OrganizationMapping = null) : IRequest<AdministrationResult>;
