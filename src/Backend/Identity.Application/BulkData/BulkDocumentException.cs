namespace Identity.Application.BulkData;

/// <summary>Structural rejection of an uploaded workbook, before any row is considered.</summary>
public sealed class BulkDocumentException : Exception
{
    public BulkDocumentException(BulkDocumentError error, string message)
        : base(message) => Error = error;

    public BulkDocumentError Error { get; }
}
