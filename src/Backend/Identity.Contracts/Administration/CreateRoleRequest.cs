using System.ComponentModel.DataAnnotations;

namespace Identity.Contracts.Administration;

public sealed record CreateRoleRequest(
    [property: Required, StringLength(100, MinimumLength = 1)] string RoleCode,
    [property: Required, StringLength(200, MinimumLength = 1)] string RoleName,
    [property: StringLength(1000)] string? Description,
    bool IsSystem);
