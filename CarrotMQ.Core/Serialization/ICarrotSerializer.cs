namespace CarrotMQ.Core.Serialization;

/// <summary>
/// Represents a serializer for serializing and deserializing objects.
/// </summary>
/// <remarks>
/// This is the serializer that will be used to serialize and deserialize the message you defined:
/// <list type="bullet">
///     <item>
///         <see cref="Dto.IEvent{TEvent,TExchangeEndPoint}" />
///     </item>
///     <item>
///         <see cref="Dto.ICommand{TCommand,TResponse,TEndPointDefinition}" />
///     </item>
///     <item>
///         <see cref="Dto.IQuery{TQuery,TResponse,TEndPointDefinition}" />
///     </item>
///     <item>
///         <see cref="Dto.ICustomRoutingEvent{TEvent}" />
///     </item>
/// </list>
/// </remarks>
public interface ICarrotSerializer
{
    /// <summary>
    /// Serialize object of type T.
    /// </summary>
    public string Serialize<T>(T obj) where T : notnull;

    /// <summary>
    /// Deserialize object into type T.
    /// </summary>
    public T? Deserialize<T>(string dataString);
}