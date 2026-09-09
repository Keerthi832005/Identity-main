using Identity.Application.Administration;
using Identity.Application.Messaging;

namespace Identity.Application.BulkData;

public sealed record GetBulkBatchQuery(
    Guid BatchKey,
    int Skip,
    int Take,
    BulkRowFilter Filter,
    AdministrationContext Context) : IRequest<BulkBatchPage>;
