using CarrotMQ.Core.Dto.Internals;

namespace CarrotMQ.Core.Configuration;

/// <summary>
/// Generic representation of a Handler for configuring the bindings between exchanges and queues.
/// where the message type is specified by <typeparamref name="TMessage" /> and the response type by
/// <typeparamref name="TResponse" />.
/// </summary>
/// <typeparam name="TMessage">The type of the message associated with the binding.</typeparam>
/// <typeparam name="TResponse">The type of the response associated with the binding.</typeparam>
public sealed class Handler<TMessage, TResponse>
    where TMessage : _IMessage<TMessage, TResponse>
    where TResponse : class
{
    /// 
    private readonly BindingCollection _bindingCollection;

    internal Handler(BindingCollection bindingCollection)
    {
        _bindingCollection = bindingCollection;
    }

    /// <summary>
    /// Bind the given <paramref name="exchange" /> to the given <paramref name="queue" /> using the <typeparamref name="TMessage" /> as routing key
    /// </summary>
    public Handler<TMessage, TResponse> BindTo(string exchange, string queue)
    {
        _bindingCollection.AddBinding(new BindingConfiguration<TMessage>(exchange, queue));

        return this;
    }
}