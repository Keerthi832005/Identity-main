namespace Identity.Application.BulkData;

/// <summary>Serializes staged cell values and errors for storage. Never carries secret material.</summary>
public interface IBulkRowSerializer
{
    string SerializeValues(IReadOnlyDictionary<string, string?> values);
    IReadOnlyDictionary<string, string?> DeserializeValues(string payload);
    string SerializeErrors(IReadOnlyList<BulkCellError> errors);
    IReadOnlyList<BulkCellError> DeserializeErrors(string? payload);
}
