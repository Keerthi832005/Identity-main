namespace Identity.Application.BulkData;

public enum BulkDocumentError
{
    NotAWorkbook,
    MetadataMissing,
    EntityMismatch,
    TemplateVersionMismatch,
    HeaderRowMissing,
    RequiredColumnMissing,
    TooLarge,
    TooManySheets,
    TooManyRows,
}
