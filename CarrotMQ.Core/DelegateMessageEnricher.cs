using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarrotMQ.Core;

/// <summary>
/// Wraps a delegate in an <see cref="IMessageEnricher" />
/// </summary>
internal sealed class DelegateMessageEnricher : IMessageEnricher
{
    private readonly Action<object, Context, CancellationToken> _enricherDelegate;

    /// <inheritdoc cref="DelegateMessageEnricher" />
    public DelegateMessageEnricher(Action<object, Context, CancellationToken> enricherDelegate)
    {
        _enricherDelegate = enricherDelegate;
    }

    /// <inheritdoc />
    public Task EnrichMessageAsync(object message, Context context, CancellationToken cancellationToken)
    {
        _enricherDelegate(message, context, cancellationToken);

        return Task.CompletedTask;
    }
}