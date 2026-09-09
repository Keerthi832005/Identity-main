namespace Identity.Application.Administration;

/// <summary>Explicit replacement. Omit this object to preserve an existing mapping.</summary>
public sealed record UserOrganizationMapping(long? DepartmentId, long? TeamId, long? BranchId = null);
