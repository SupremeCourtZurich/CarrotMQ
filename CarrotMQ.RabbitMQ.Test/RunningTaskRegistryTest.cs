using CarrotMQ.RabbitMQ.MessageProcessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class RunningTaskRegistryTest
{
    private readonly RunningTaskRegistry _registry = new();

    [TestMethod]
    public async Task CompleteAdding_Returns_When_Not_Tasks_Added()
    {
        await _registry.CompleteAddingAsync().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task TryAdd_Returns_False_After_CompleteAdding_With_No_Tasks()
    {
        await _registry.CompleteAddingAsync().ConfigureAwait(false);

        Assert.IsFalse(_registry.TryAdd(CreateEmptyBasicDeliverEventArgs()));
    }

    [TestMethod]
    public async Task TryAdd_Returns_False_After_CompleteAdding_With_Tasks()
    {
        var message1 = CreateEmptyBasicDeliverEventArgs();
        _registry.TryAdd(message1);

        var completeAddingTask = _registry.CompleteAddingAsync();

        _registry.Remove(message1);

        Assert.IsFalse(_registry.TryAdd(CreateEmptyBasicDeliverEventArgs()));

        await completeAddingTask.ConfigureAwait(false);
    }

    [TestMethod]
    public async Task TryAdd_Returns_True_After_CompleteAdding_When_Tasks_Still_Running()
    {
        var message1 = CreateEmptyBasicDeliverEventArgs();
        _registry.TryAdd(message1);
        var completeAddingTask = _registry.CompleteAddingAsync();

        var message2 = CreateEmptyBasicDeliverEventArgs();
        Assert.IsTrue(_registry.TryAdd(message2));

        _registry.Remove(message1);
        _registry.Remove(message2);

        await completeAddingTask.ConfigureAwait(false);
    }

    [TestMethod]
    public async Task CompleteAdding_Waits_Until_AllTasksRemoved()
    {
        var basicDeliveryEventArgs = CreateEmptyBasicDeliverEventArgs();
        _registry.TryAdd(basicDeliveryEventArgs);
        var completeAddingTask = _registry.CompleteAddingAsync();

        // Check our completeAdding task is still running
        Assert.AreNotEqual(TaskStatus.Canceled, completeAddingTask.Status);
        Assert.AreNotEqual(TaskStatus.Faulted, completeAddingTask.Status);
        Assert.AreNotEqual(TaskStatus.RanToCompletion, completeAddingTask.Status);

        _registry.Remove(basicDeliveryEventArgs);

        await completeAddingTask.ConfigureAwait(false);
    }

    private BasicDeliverEventArgs CreateEmptyBasicDeliverEventArgs()
    {
        return new BasicDeliverEventArgs(
            "1",
            1,
            false,
            "exchange",
            "routingKey",
            new ReadOnlyBasicProperties(ReadOnlySpan<byte>.Empty),
            ReadOnlyMemory<byte>.Empty);
    }
}