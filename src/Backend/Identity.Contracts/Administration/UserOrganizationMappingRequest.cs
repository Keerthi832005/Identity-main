using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

/// <summary>Omit to preserve; send both IDs null to clear the assignment.</summary>
public sealed record UserOrganizationMappingRequest(
    [property: Range(typeof(long), "1", "9223372036854775807")] long? DepartmentId,
    [property: Range(typeof(long), "1", "9223372036854775807")] long? TeamId,
    [property: Range(typeof(long), "1", "9223372036854775807")] long? BranchId = null)
{
    public static ValidationResult? Validate(UserOrganizationMappingRequest? mapping) =>
        mapping?.DepartmentId is <= 0 || mapping?.TeamId is <= 0 || mapping?.BranchId is <= 0
            ? new ValidationResult("Department, team and branch IDs must be positive.")
            : mapping is { DepartmentId: null, TeamId: not null }
                ? new ValidationResult("Select a department before assigning a team.")
                : ValidationResult.Success;
}
