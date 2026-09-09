namespace Identity.AdminCli;

public sealed record ValidateApplicationManifestOptions(string ManifestPath)
{
    public static ValidateApplicationManifestOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count != 2 || !string.Equals(args[0], "--manifest", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Manifest validation requires exactly '--manifest PATH'.");
        }

        return string.IsNullOrWhiteSpace(args[1])
            ? throw new ArgumentException("Manifest validation path is required.")
            : new ValidateApplicationManifestOptions(args[1]);
    }
}
