using System.Globalization;

namespace Identity.AdminCli;

public sealed record SeedDefaultEmployeesOptions(long ActorUserId, bool Apply)
{
    public static SeedDefaultEmployeesOptions Parse(IReadOnlyList<string> args)
    {
        long? actor = null;
        var apply = false;
        for (var index = 0; index < args.Count; index++)
        {
            if (args[index] == "--apply" && !apply) { apply = true; continue; }
            if (args[index] == "--actor-user-id" && actor is null && index + 1 < args.Count
                && long.TryParse(args[++index], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && id > 0) { actor = id; continue; }
            throw new ArgumentException("Use --actor-user-id with a positive ID and optional --apply; no duplicate switches.");
        }
        return new(actor ?? throw new ArgumentException("--actor-user-id is required."), apply);
    }
}
