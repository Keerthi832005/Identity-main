using System.Text.Json;
using System.Text.RegularExpressions;
using Identity.Domain.Entities;

namespace Identity.AdminCli;

public sealed record DefaultEmployee(string PersonnelNumber, string Name, string? Email = null);

public static class DefaultEmployeeRoster
{
    public static IReadOnlyList<DefaultEmployee> Load()
    {
        using var stream = typeof(DefaultEmployeeRoster).Assembly
            .GetManifestResourceStream("Identity.AdminCli.DefaultEmployees.json")
            ?? throw new InvalidOperationException("Default employee roster is missing.");
        var rows = JsonSerializer.Deserialize<DefaultEmployee[]>(stream)
            ?? throw new InvalidOperationException("Default employee roster is empty.");
        Validate(rows);
        return rows;
    }

    public static void Validate(IReadOnlyList<DefaultEmployee> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count is 0 or > 10000) throw new ArgumentException("Invalid roster size.");
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row is null || string.IsNullOrWhiteSpace(row.PersonnelNumber)
                || !Regex.IsMatch(row.PersonnelNumber, "^IND[CE][0-9]{5}$", RegexOptions.CultureInvariant)
                || !codes.Add(row.PersonnelNumber))
                throw new ArgumentException("Roster contains an invalid or duplicate personnel number.");
            _ = UserAccount.Create(row.PersonnelNumber, row.Name, DateTime.UtcNow, row.Email);
        }
    }
}
