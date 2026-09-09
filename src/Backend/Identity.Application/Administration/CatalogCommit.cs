using System.Globalization;

namespace Identity.Application.Administration;

/// <summary>Shared reading of staged catalog values. Normalisation already happened at the reader.</summary>
internal static class CatalogCommit
{
    internal static async Task<long> ApplicationId(
        ICatalogCodeResolver resolver,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken)
    {
        var code = Required(values, CatalogBulkDescriptors.ApplicationCode);
        var applications = await resolver
            .FindApplicationIdsByCodes([code], cancellationToken)
            .ConfigureAwait(false);
        return applications.TryGetValue(code, out var id)
            ? id
            : throw new AdministrationException($"Application {code} no longer exists.");
    }

    internal static string Required(IReadOnlyDictionary<string, string?> values, string columnId) =>
        Optional(values, columnId)
        ?? throw new AdministrationException($"Staged row is missing {columnId}.");

    internal static string? Optional(IReadOnlyDictionary<string, string?> values, string columnId) =>
        values.TryGetValue(columnId, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    internal static int Number(IReadOnlyDictionary<string, string?> values, string columnId) =>
        Optional(values, columnId) is { } value
        && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? (int)number
            : 0;

    internal static bool Flag(IReadOnlyDictionary<string, string?> values, string columnId) =>
        string.Equals(Optional(values, columnId), "true", StringComparison.OrdinalIgnoreCase);
}
