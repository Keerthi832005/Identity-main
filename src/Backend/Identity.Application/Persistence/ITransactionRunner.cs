namespace Identity.Application.Persistence;

public interface ITransactionRunner
{
    Task<T> Execute<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
