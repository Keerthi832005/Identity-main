using Identity.Application.BulkData;

namespace Identity.Application.Administration;

public sealed class RolesBulkValidator(ICatalogCodeResolver resolver)
    : CatalogBulkValidator(resolver)
{
    public override string EntityKey => CatalogBulkDescriptors.RolesKey;

    protected override bool Exists(CatalogBatch batch, BulkRow row, long applicationId) =>
        Value(row, CatalogBulkDescriptors.RoleCode) is { } code
        && batch.RoleExists(applicationId, code);

    protected override ValueTask Check(
        CatalogBatch batch,
        BulkRow row,
        long applicationId,
        List<BulkCellError> errors,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
