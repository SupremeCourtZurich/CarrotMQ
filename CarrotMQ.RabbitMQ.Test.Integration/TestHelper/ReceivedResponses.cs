using System.Threading.Channels;
using CarrotMQ.Core;

namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

public sealed class ReceivedResponses
{
    private readonly Channel<Response> _messageChannel = Channel.CreateBounded<Response>(10);

    public async ValueTask WriteAsync(Response item, CancellationToken cancellationToken)
    {
        await _messageChannel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Response> ReadAsync(CancellationToken cancellationToken)
    {
        return await _messageChannel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }
}

#pragma warning disable MA0048 // File name must match type name
public record Response
#pragma warning restore MA0048 // File name must match type name
{
    public int Id { get; set; }

    public int StatusCode { get; set; }

    public CarrotError? Error { get; set; } = null!;
}