namespace Identity.AdminCli;

public sealed record BootstrapOptions(
    string EmployeeCode,
    string DisplayName,
    string ApplicationCode,
    string ApplicationName,
    string ClientId,
    string Audience,
    string? Email = null)
{
    public static BootstrapOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Bootstrap switches must be supplied as name/value pairs.");
            }

            var name = args[index];
            if (!AllowedSwitches.Contains(name) || !values.TryAdd(name, args[index + 1]))
            {
                throw new ArgumentException($"Bootstrap switch '{name}' is unknown or duplicated.");
            }
        }

        return new BootstrapOptions(
            Required(values, "--employee-code"),
            Required(values, "--display-name"),
            Value(values, "--application-code", "iam-administration"),
            Value(values, "--application-name", "Identity Administration"),
            Required(values, "--client-id"),
            Value(values, "--audience", "urn:identity:administration"),
            values.GetValueOrDefault("--email"));
    }

    private static readonly HashSet<string> AllowedSwitches = new(StringComparer.Ordinal)
    {
        "--employee-code",
        "--display-name",
        "--application-code",
        "--application-name",
        "--client-id",
        "--audience",
        "--email",
    };

    private static string Required(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Bootstrap switch '{name}' is required.");

    private static string Value(
        IReadOnlyDictionary<string, string> values,
        string name,
        string fallback) => values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
}
