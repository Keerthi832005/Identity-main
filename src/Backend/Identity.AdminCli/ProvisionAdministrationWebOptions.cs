namespace Identity.AdminCli;

public sealed record ProvisionAdministrationWebOptions(string EmployeeCode)
{
    public static ProvisionAdministrationWebOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count != 2 || args[0] != "--employee-code" || string.IsNullOrWhiteSpace(args[1]))
        {
            throw new ArgumentException(
                "Supply exactly one non-empty --employee-code value.");
        }

        return new ProvisionAdministrationWebOptions(args[1].Trim());
    }
}
