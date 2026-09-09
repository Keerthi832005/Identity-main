using Identity.Application.Administration;
using Identity.Application.Messaging;

namespace Identity.Application.BulkData;

public sealed record DiscardBulkBatchCommand(
    Guid BatchKey,
    AdministrationContext Context) : IRequest<BulkDiscardResult>;
