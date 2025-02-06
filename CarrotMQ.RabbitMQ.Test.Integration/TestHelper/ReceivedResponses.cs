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

public record Response
{
    public int Id { get; set; }

    public int StatusCode { get; set; }

    public CarrotError? Error { get; set; } = null!;
}