using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record CreateUserCommand(
    string EmployeeCode,
    string DisplayName,
    AdministrationContext Context,
    string? Email = null,
    long? ManagerUserId = null,
    UserOrganizationMapping? OrganizationMapping = null) : IRequest<AdministrationResult>;
