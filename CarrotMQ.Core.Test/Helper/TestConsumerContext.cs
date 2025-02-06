using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.Test.Helper;

internal sealed class TestConsumerContext
{
    public static ConsumerContext GetConsumerContext()
    {
        return new ConsumerContext(null, null, default, null, Guid.Empty, null, DateTimeOffset.Now);
    }

    public static ConsumerContext CreateConsumerContext(CarrotHeader carrotHeader)
    {
        var context = new ConsumerContext(
            carrotHeader.InitialUserName,
            carrotHeader.InitialServiceName,
            carrotHeader.MessageProperties,
            carrotHeader.CustomHeader,
            carrotHeader.MessageId,
            carrotHeader.CorrelationId,
            carrotHeader.CreatedAt);

        return context;
    }
}