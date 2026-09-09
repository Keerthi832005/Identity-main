namespace Identity.Contracts.Authentication;

public sealed record OperationResponse(bool Succeeded, string? FailureCode);
