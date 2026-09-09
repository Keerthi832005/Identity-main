using System.Globalization;

namespace Identity.AdminCli;

public sealed record ProvisionApplicationOptions(
    string ManifestPath,
    long ActorUserId,
    string AdministrationApplicationCode)
{
    public static ProvisionApplicationOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Provisioning switches must be supplied as name/value pairs.");
            }

            var name = args[index];
            if (!AllowedSwitches.Contains(name) || !values.TryAdd(name, args[index + 1]))
            {
                throw new ArgumentException(
                    $"Provisioning switch '{name}' is unknown or duplicated.");
            }
        }

        var actorValue = Required(values, "--actor-user-id");
        if (!long.TryParse(actorValue, NumberStyles.None, CultureInfo.InvariantCulture, out var actorUserId)
            || actorUserId <= 0)
        {
            throw new ArgumentException("Provisioning switch '--actor-user-id' must be a positive integer.");
        }

        return new ProvisionApplicationOptions(
            Required(values, "--manifest"),
            actorUserId,
            Value(values, "--administration-application-code", "iam-administration"));
    }

    private static readonly HashSet<string> AllowedSwitches = new(StringComparer.Ordinal)
    {
        "--manifest",
        "--actor-user-id",
        "--administration-application-code",
    };

    private static string Required(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Provisioning switch '{name}' is required.");

    private static string Value(
        IReadOnlyDictionary<string, string> values,
        string name,
        string fallback) => values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
}
