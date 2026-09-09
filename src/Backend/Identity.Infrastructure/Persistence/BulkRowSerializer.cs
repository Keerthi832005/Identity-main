using System.Text.Json;
using System.Text.Json.Serialization;
using Identity.Application.BulkData;

namespace Identity.Infrastructure.Persistence;

public sealed class BulkRowSerializer : IBulkRowSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    public string SerializeValues(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return JsonSerializer.Serialize(values, Options);
    }

    public IReadOnlyDictionary<string, string?> DeserializeValues(string payload) =>
        JsonSerializer.Deserialize<Dictionary<string, string?>>(payload, Options)
        ?? new Dictionary<string, string?>();

    public string SerializeErrors(IReadOnlyList<BulkCellError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return JsonSerializer.Serialize(errors, Options);
    }

    public IReadOnlyList<BulkCellError> DeserializeErrors(string? payload) =>
        string.IsNullOrWhiteSpace(payload)
            ? []
            : JsonSerializer.Deserialize<List<BulkCellError>>(payload, Options) ?? [];
}
