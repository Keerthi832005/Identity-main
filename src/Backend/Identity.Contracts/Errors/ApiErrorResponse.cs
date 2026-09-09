namespace Identity.Contracts.Errors;

public sealed record ApiErrorResponse(
    string Type,
    string Title,
    int Status,
    string Code,
    string CorrelationId);
