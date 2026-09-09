using Identity.Application.Administration;
using Identity.Application.Messaging;

namespace Identity.Application.BulkData;

public sealed record CommitBulkBatchCommand(
    Guid BatchKey,
    AdministrationContext Context) : IRequest<BulkCommitResult>;
