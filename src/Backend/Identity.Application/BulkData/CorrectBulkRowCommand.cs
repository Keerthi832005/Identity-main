using Identity.Application.Administration;
using Identity.Application.Messaging;

namespace Identity.Application.BulkData;

public sealed record CorrectBulkRowCommand(
    Guid BatchKey,
    int SourceRowNumber,
    IReadOnlyDictionary<string, string?> Values,
    AdministrationContext Context) : IRequest<BulkStagedRow>;
