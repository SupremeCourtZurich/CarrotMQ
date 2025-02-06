using System.Collections.Generic;

namespace CarrotMQ.Core;

/// <summary>
/// Contains additional infos when sending/publishing a message with <see cref="ICarrotClient" />.
/// </summary>
public class Context
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Context" /> class.
    /// </summary>
    public Context(
        string? initialUserName = null,
        string? initialServiceName = null,
        MessageProperties? messageProperties = null,
        IDictionary<string, string>? customHeader = null)
    {
        InitialUserName = initialUserName;
        InitialServiceName = initialServiceName;
        MessageProperties = messageProperties ?? MessageProperties.Default;
        CustomHeader = customHeader ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Context" /> class.
    /// </summary>
    /// <param name="ttl">TTL in milliseconds to set <see cref="MessageProperties.Ttl" /></param>
    /// <param name="context">All values are copied from this context except <see cref="MessageProperties.Ttl" /></param>
    public Context(int ttl, Context? context = null)
    {
        InitialUserName = context?.InitialUserName;
        InitialServiceName = context?.InitialServiceName;
        CustomHeader = context?.CustomHeader ?? new Dictionary<string, string>();

        if (context != null)
        {
            var messageProperties = context.MessageProperties;
            messageProperties.Ttl = ttl;
            MessageProperties = messageProperties;
        }
        else
        {
            MessageProperties = new MessageProperties { Ttl = ttl };
        }
    }

    /// <summary>
    /// Gets the name of the user sending the initial message.
    /// </summary>
    public string? InitialUserName { get; }

    /// <summary>
    /// Gets the name of the service or application sending the initial message.
    /// </summary>
    public string? InitialServiceName { get; }

    /// <summary>
    /// Gets the message properties. The default is <see cref="MessageProperties.Default" />
    /// </summary>
    public MessageProperties MessageProperties { get; }

    /// <summary>
    /// Additional header data, e.g. tracing ids.
    /// </summary>
    public IDictionary<string, string> CustomHeader { get; }
}