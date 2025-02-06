using System.Collections.Generic;

namespace CarrotMQ.RabbitMQ.Configuration.Exchanges;

internal class ExchangeConfiguration
{
    public ExchangeConfiguration(string name, string type)
    {
        Name = name;
        Type = type;
    }

    /// <summary>
    /// Name of the exchange.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Type of the exchange.
    /// </summary>
    internal string Type { get; }

    /// <summary>
    /// Flag indicating whether the exchange is durable.<br />
    /// Defaults to <see langword="true"/>.
    /// </summary>
    internal bool Durable { get; set; } = true;

    /// <summary>
    /// Flag indicating whether the exchange is automatically deleted when the last bound queue is unbound.<br />
    /// Defaults to <see langword="false"/>.
    /// </summary>
    internal bool AutoDelete { get; set; }

    /// <summary>
    /// Additional arguments for configuring the exchange.
    /// </summary>
    internal IDictionary<string, object?> Arguments { get; set; } = new Dictionary<string, object?>();
}