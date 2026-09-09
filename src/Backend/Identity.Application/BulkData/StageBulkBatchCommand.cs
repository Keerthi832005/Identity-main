using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Domain.Enums;

namespace Identity.Application.BulkData;

public sealed record StageBulkBatchCommand(
    string EntityKey,
    BulkImportSource Source,
    string? FileName,
    IReadOnlyList<BulkRow> Rows,
    AdministrationContext Context) : IRequest<BulkBatchSummary>;
