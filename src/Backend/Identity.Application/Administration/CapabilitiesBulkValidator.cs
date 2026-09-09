using Identity.Application.BulkData;

namespace Identity.Application.Administration;

public sealed class CapabilitiesBulkValidator(ICatalogCodeResolver resolver)
    : CatalogBulkValidator(resolver)
{
    public override string EntityKey => CatalogBulkDescriptors.CapabilitiesKey;

    protected override bool Exists(CatalogBatch batch, BulkRow row, long applicationId) =>
        Value(row, CatalogBulkDescriptors.CapabilityCode) is { } code
        && batch.CapabilityExists(applicationId, code);

    protected override ValueTask Check(
        CatalogBatch batch,
        BulkRow row,
        long applicationId,
        List<BulkCellError> errors,
        CancellationToken cancellationToken)
    {
        /* Capabilities attach to an existing module. A module created in the same import is a
           separate file, so it must be committed before this one. */
        var module = Value(row, CatalogBulkDescriptors.ModuleCode);
        if (module is not null && !batch.ModuleExists(applicationId, module))
        {
            errors.Add(new BulkCellError(
                row.SourceRowNumber,
                CatalogBulkDescriptors.ModuleCode,
                UnknownModule,
                $"No module with code {module} in this application. Import the modules file first."));
        }

        return ValueTask.CompletedTask;
    }
}
