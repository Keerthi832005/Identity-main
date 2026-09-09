using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Application.Messaging;

public sealed class RequestDispatcher(IServiceProvider serviceProvider) : IRequestDispatcher
{
    public ValueTask<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoker = InvokerCache<TResponse>.Invokers.GetOrAdd(
            request.GetType(),
            static requestType => CreateInvoker<TResponse>(requestType));
        return invoker.Invoke(serviceProvider, request, cancellationToken);
    }

    private static HandlerInvoker<TResponse> CreateInvoker<TResponse>(Type requestType)
    {
        var invokerType = typeof(HandlerInvoker<,>).MakeGenericType(requestType, typeof(TResponse));
        return (HandlerInvoker<TResponse>)Activator.CreateInstance(invokerType)!;
    }

    private static class InvokerCache<TResponse>
    {
        internal static readonly ConcurrentDictionary<Type, HandlerInvoker<TResponse>> Invokers = new();
    }

    private abstract class HandlerInvoker<TResponse>
    {
        internal abstract ValueTask<TResponse> Invoke(
            IServiceProvider provider,
            IRequest<TResponse> request,
            CancellationToken cancellationToken);
    }

    private sealed class HandlerInvoker<TRequest, TResponse> : HandlerInvoker<TResponse>
        where TRequest : IRequest<TResponse>
    {
        internal override ValueTask<TResponse> Invoke(
            IServiceProvider provider,
            IRequest<TResponse> request,
            CancellationToken cancellationToken) => provider
                .GetRequiredService<IRequestHandler<TRequest, TResponse>>()
                .Handle((TRequest)request, cancellationToken);
    }
}
