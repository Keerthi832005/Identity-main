namespace Identity.Application.BulkData;

/// <summary>Stable codes. The browser keys off these; the message text is free to change.</summary>
public static class BulkErrorCodes
{
    public const string Required = "cell.required";
    public const string Malformed = "cell.malformed";
    public const string NotAllowed = "cell.not-allowed";
    public const string TooLong = "cell.too-long";
    public const string DuplicateInBatch = "row.duplicate-in-batch";
    public const string KeyMissing = "row.key-missing";
}
