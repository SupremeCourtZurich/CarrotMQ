using CarrotMQ.Core.Dto;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;

namespace CarrotMQ.RabbitMQ.Test.Load;

internal static class Destinations
{
    // Always exists; with an unbound routing key the broker confirms at once and stores nothing.
    public const string ExistingExchange = "amq.direct";
    public const string MissingExchange = "load.missing.exchange";
    public const string QueryExchange = "load.query.exchange";
    public const string QueryQueue = "load.query.queue";
}

public sealed class LoadEvent : ICustomRoutingEvent<LoadEvent>
{
    public LoadEvent(string exchange)
    {
        Exchange = exchange;
    }

    public string Exchange { get; set; }

    public string RoutingKey { get; set; } = "load.unbound";
}

public sealed class LoadExchange : ExchangeEndPoint
{
    public LoadExchange() : base(Destinations.QueryExchange)
    {
    }
}

public sealed class MissingExchangeEndPoint : ExchangeEndPoint
{
    public MissingExchangeEndPoint() : base(Destinations.MissingExchange)
    {
    }
}

public sealed class LoadQuery : IQuery<LoadQuery, LoadQuery.Response, LoadExchange>
{
    public int Id { get; set; }

    public sealed class Response
    {
        public int Id { get; set; }
    }
}

public sealed class MissingExchangeQuery : IQuery<MissingExchangeQuery, LoadQuery.Response, MissingExchangeEndPoint>;

public sealed class LoadQueryHandler : QueryHandlerBase<LoadQuery, LoadQuery.Response>
{
    public override Task<IHandlerResult> HandleAsync(LoadQuery query, ConsumerContext consumerContext, CancellationToken cancellationToken)
    {
        return Task.FromResult(Ok(new LoadQuery.Response { Id = query.Id }));
    }
}
