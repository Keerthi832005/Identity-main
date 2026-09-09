using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record UpdateUserProfileRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string DisplayName,
    [property: EmailAddress, StringLength(254)] string? Email,
    [property: Range(typeof(long), "1", "9223372036854775807")] long? ManagerUserId,
    [property: CustomValidation(typeof(UserOrganizationMappingRequest), nameof(UserOrganizationMappingRequest.Validate))]
    UserOrganizationMappingRequest? OrganizationMapping = null);
