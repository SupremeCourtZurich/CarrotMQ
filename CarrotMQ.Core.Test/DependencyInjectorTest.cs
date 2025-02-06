using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace CarrotMQ.Core.Test;

[TestClass]
public class DependencyInjectorTest
{
    [TestInitialize]
    public void Setup()
    {
        AsyncDisposableSerializer.AllDisposed.Clear();
        DisposableSerializer.AllDisposed.Clear();
        TestTransport.AllDisposed.Clear();
    }

    [TestMethod]
    public async Task Get_AsyncDisposable_Transport()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.AddScoped<ITransport, TestTransport>();
        await using (var serviceProvider = serviceCollection.BuildServiceProvider())
        {
            IDependencyInjector dependencyInjector = new DependencyInjector(serviceProvider);

            _ = dependencyInjector.GetTransport();

            await dependencyInjector.DisposeAsync();
        }

        Assert.AreEqual(1, TestTransport.AllDisposed.Count);
        Assert.IsTrue(TestTransport.AllDisposed.All(entry => entry.Value), $"Not all {nameof(TestTransport)} have been disposed");
    }

    [TestMethod]
    public async Task Get_Scoped_AsyncDisposable_Transport()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.AddScoped<ITransport, TestTransport>();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        IDependencyInjector dependencyInjector = new DependencyInjector(serviceProvider);

        await using (var scope = dependencyInjector.CreateAsyncScope())
        {
            _ = scope.GetTransport();
        }

        await dependencyInjector.DisposeAsync();

        Assert.AreEqual(1, TestTransport.AllDisposed.Count);
        Assert.IsTrue(TestTransport.AllDisposed.All(entry => entry.Value), $"Not all {nameof(TestTransport)} have been disposed");
    }

    [TestMethod]
    public async Task Disposable_Serializer_IsDisposed()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.AddScoped<ICarrotSerializer, DisposableSerializer>();
        await using (var serviceProvider = serviceCollection.BuildServiceProvider())
        {
            IDependencyInjector dependencyInjector = new DependencyInjector(serviceProvider);

            _ = dependencyInjector.GetCarrotSerializer();

            await dependencyInjector.DisposeAsync();
        }

        Assert.AreEqual(1, DisposableSerializer.AllDisposed.Count);
        Assert.IsTrue(DisposableSerializer.AllDisposed.All(entry => entry.Value), $"Not all {nameof(DisposableSerializer)} have been disposed");
    }

    [TestMethod]
    public async Task Scoped_Disposable_Serializer_IsDisposed()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.AddScoped<ICarrotSerializer, DisposableSerializer>();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        IDependencyInjector dependencyInjector = new DependencyInjector(serviceProvider);

        await using (var scope = dependencyInjector.CreateAsyncScope())
        {
            _ = scope.GetCarrotSerializer();
        }

        Assert.AreEqual(1, DisposableSerializer.AllDisposed.Count);
        Assert.IsTrue(DisposableSerializer.AllDisposed.All(entry => entry.Value), $"Not all {nameof(DisposableSerializer)} have been disposed");

        await dependencyInjector.DisposeAsync();
    }

    [TestMethod]
    public async Task AsyncDisposable_Serializer_IsDisposed()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.AddScoped<ICarrotSerializer, AsyncDisposableSerializer>();
        await using (var serviceProvider = serviceCollection.BuildServiceProvider())
        {
            IDependencyInjector dependencyInjector = new DependencyInjector(serviceProvider);

            _ = dependencyInjector.GetCarrotSerializer();

            await dependencyInjector.DisposeAsync();
        }

        Assert.AreEqual(1, AsyncDisposableSerializer.AllDisposed.Count);
        Assert.IsTrue(AsyncDisposableSerializer.AllDisposed.All(entry => entry.Value), $"Not all {nameof(DisposableSerializer)} have been disposed");
    }

    [TestMethod]
    public async Task Scoped_AsyncDisposable_Serializer_IsDisposed()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.AddScoped<ICarrotSerializer, AsyncDisposableSerializer>();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        IDependencyInjector dependencyInjector = new DependencyInjector(serviceProvider);

        await using (var scope = dependencyInjector.CreateAsyncScope())
        {
            _ = scope.GetCarrotSerializer();
        }

        Assert.AreEqual(1, AsyncDisposableSerializer.AllDisposed.Count);
        Assert.IsTrue(AsyncDisposableSerializer.AllDisposed.All(entry => entry.Value), $"Not all {nameof(DisposableSerializer)} have been disposed");

        await dependencyInjector.DisposeAsync();
    }

    public class DisposableSerializer : ICarrotSerializer, IDisposable
    {
        public static readonly IDictionary<DisposableSerializer, bool> AllDisposed = new Dictionary<DisposableSerializer, bool>();

        public DisposableSerializer()
        {
            AllDisposed.Add(this, false);
        }

        public string Serialize<T>(T obj) where T : notnull
        {
            return string.Empty;
        }

        public T? Deserialize<T>(string dataString)
        {
            return default;
        }

        public void Dispose()
        {
            AllDisposed[this] = true;
        }
    }

    public sealed class AsyncDisposableSerializer : ICarrotSerializer, IAsyncDisposable
    {
        public static readonly IDictionary<AsyncDisposableSerializer, bool> AllDisposed = new Dictionary<AsyncDisposableSerializer, bool>();

        public AsyncDisposableSerializer()
        {
            AllDisposed.Add(this, false);
        }

        public string Serialize<T>(T obj) where T : notnull
        {
            return string.Empty;
        }

        public T? Deserialize<T>(string dataString)
        {
            return default;
        }

        public ValueTask DisposeAsync()
        {
            AllDisposed[this] = true;

            return new ValueTask(Task.CompletedTask);
        }
    }

    public sealed class TestTransport : ITransport
    {
        public static readonly IDictionary<TestTransport, bool> AllDisposed = new Dictionary<TestTransport, bool>();

        public TestTransport()
        {
            AllDisposed.Add(this, false);
        }

        public ValueTask DisposeAsync()
        {
            AllDisposed[this] = true;

            return new ValueTask(Task.CompletedTask);
        }

        public Task SendAsync(CarrotMessage message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<CarrotMessage> SendReceiveAsync(CarrotMessage message, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CarrotMessage());
        }

        public Task SubscribeAsync(string exchange, string routingKey, string calledMethod)
        {
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string calledMethod)
        {
            return Task.CompletedTask;
        }

        public Task StartConsumingAsync()
        {
            return Task.CompletedTask;
        }

        public Task StopConsumingAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            AllDisposed[this] = true;
        }
    }
}