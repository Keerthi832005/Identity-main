namespace Identity.Application.Authentication;

public sealed record OperationResult(bool Succeeded, AuthenticationFailureCode? FailureCode)
{
    public static OperationResult Success { get; } = new(true, null);
    public static OperationResult Rejected(AuthenticationFailureCode failureCode) => new(false, failureCode);
}
