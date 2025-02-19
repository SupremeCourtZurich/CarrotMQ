using System.Threading.Channels;

namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

public sealed class ReceivedMessages
{
    private readonly Channel<int> _messageChannel = Channel.CreateBounded<int>(10);

    public async ValueTask WriteAsync(int item, CancellationToken cancellationToken)
    {
        await _messageChannel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> ReadAsync(CancellationToken cancellationToken)
    {
        return await _messageChannel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int[]> ReadAllAsync(int count, CancellationToken cancellationToken)
    {
        var allItems = new int[count];

        for (var i = 0; i < count; i++)
        {
            allItems[i] = await ReadAsync(cancellationToken).ConfigureAwait(false);
        }

        return allItems;
    }


}