using System;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Represents an internal class for handling response callbacks.
/// </summary>
/// <typeparam name="TRequest">The type of the request associated with the response.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
internal class ResponseCallback<TRequest, TResponse> : IMessageCallback where TRequest : _IRequest<TRequest, TResponse> where TResponse : class
{
    private readonly Func<CarrotResponse<TRequest, TResponse>, ConsumerContext, CancellationToken, Task> _callback;
    private readonly ICarrotSerializer _serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseCallback{TRequest, TResponse}" /> class.
    /// </summary>
    /// <param name="callback">The callback function to be executed when handling the response.</param>
    /// <param name="serializer">The serializer used to deserialize the response payload.</param>
    public ResponseCallback(
        Func<CarrotResponse<TRequest, TResponse>, ConsumerContext, CancellationToken, Task> callback,
        ICarrotSerializer serializer)
    {
        _callback = callback;
        _serializer = serializer;
    }

    public Type MessageType => typeof(TRequest);

    /// <inheritdoc />
    public Task CallAsync(CarrotMessage message, ConsumerContext consumerContext, CancellationToken cancellationToken)
    {
        var carrotResponse = _serializer.DeserializeWithNullCheck<CarrotResponse<TRequest, TResponse>>(message.Payload);

        return _callback(carrotResponse, consumerContext, cancellationToken);
    }
}