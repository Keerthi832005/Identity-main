using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record CreateUserRequest(
    [property: Required, StringLength(50, MinimumLength = 1)] string EmployeeCode,
    [property: Required, StringLength(200, MinimumLength = 1)] string DisplayName,
    [property: EmailAddress, StringLength(254)] string? Email = null,
    [property: Range(typeof(long), "1", "9223372036854775807")] long? ManagerUserId = null,
    [property: CustomValidation(typeof(UserOrganizationMappingRequest), nameof(UserOrganizationMappingRequest.Validate))]
    UserOrganizationMappingRequest? OrganizationMapping = null);
