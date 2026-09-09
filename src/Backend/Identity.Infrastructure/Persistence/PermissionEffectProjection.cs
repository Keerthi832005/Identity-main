using Identity.Domain.Enums;

namespace Identity.Infrastructure.Persistence;

internal sealed record PermissionEffectProjection(
    string CapabilityCode,
    PermissionEffect Effect);
