namespace Identity.Api.Administration;

internal static class AdministrationPolicy
{
    public const string Name = "iam-administration";
    public const string CapabilityClaim = "capability";
    public const string RequiredCapability = "iam.admin";
}
