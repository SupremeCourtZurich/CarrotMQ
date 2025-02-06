using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace CarrotMQ.RabbitMQ.MessageProcessing;

/// <summary>
/// Represents a registry for managing running tasks associated with RabbitMQ message delivery.
/// </summary>
/// <remarks>
/// The <see cref="IRunningTaskRegistry" /> interface defines methods to track and manage running tasks associated with
/// RabbitMQ message delivery.
/// </remarks>
public interface IRunningTaskRegistry
{
    /// <summary>
    /// Attempts to add a running task associated to the provided <see cref="BasicDeliverEventArgs" />.
    /// </summary>
    /// <param name="ea">The <see cref="BasicDeliverEventArgs" /> representing the running task.</param>
    /// <returns><see langword="true"/> if the running task was successfully added; otherwise, <see langword="false"/>.</returns>
    bool TryAdd(BasicDeliverEventArgs ea);

    /// <summary>
    /// Removes a running task associated to the provided <see cref="BasicDeliverEventArgs" />.
    /// </summary>
    /// <param name="ea">The <see cref="BasicDeliverEventArgs" /> representing the running task to be removed.</param>
    void Remove(BasicDeliverEventArgs ea);

    /// <summary>
    /// Marks the registry as complete, indicating that no more tasks will be added and waits for all tasks of the registry to
    /// complete.
    /// </summary>
    Task CompleteAddingAsync();
}