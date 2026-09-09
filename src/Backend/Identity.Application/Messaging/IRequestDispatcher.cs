namespace Identity.Application.Messaging;

public interface IRequestDispatcher
{
    ValueTask<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
