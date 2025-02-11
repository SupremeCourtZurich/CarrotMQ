using System;
using System.Threading.Tasks;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Implementation of the <see cref="IDependencyInjector" /> interface using the microsoft DI framework with the
/// <see cref="IServiceProvider" />
/// </summary>
internal sealed class DependencyInjector : IDependencyInjector
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceScope? _serviceScope;

    /// <summary>
    /// Initializes a new instance of the <see cref="DependencyInjector" /> class.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider" /> used for dependency resolution.</param>
    public DependencyInjector(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DependencyInjector" /> class within the context of a specific
    /// <see cref="IServiceScope" />.
    /// </summary>
    /// <param name="serviceScope">
    /// The <see cref="IServiceScope" /> within which the <see cref="DependencyInjector" /> is
    /// created.
    /// </param>
    /// <remarks>
    /// This constructor is private and is only used when creating a new scope using <see cref="CreateAsyncScope" />
    /// </remarks>
    private DependencyInjector(IServiceScope serviceScope)
    {
        _serviceScope = serviceScope;
        _serviceProvider = serviceScope.ServiceProvider;
    }

    /// <inheritdoc />
    public IDependencyInjector CreateAsyncScope()
    {
        IServiceScope scope = _serviceProvider.CreateAsyncScope();

        return new DependencyInjector(scope);
    }

    /// <inheritdoc />
    public THandler? CreateHandler<THandler, TMessage, TResponse>() where THandler : HandlerBase<TMessage, TResponse>
        where TMessage : _IMessage<TMessage, TResponse>
        where TResponse : class
    {
        return _serviceProvider.GetService<THandler>();
    }

    /// <inheritdoc />
    public ICarrotSerializer GetCarrotSerializer()
    {
        return _serviceProvider.GetRequiredService<ICarrotSerializer>();
    }

    /// <inheritdoc />
    public IMiddlewareProcessor GetMiddlewareProcessor()
    {
        return _serviceProvider.GetRequiredService<IMiddlewareProcessor>();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_serviceScope is IAsyncDisposable serviceScopeAsyncDisposable)
        {
            await serviceScopeAsyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _serviceScope?.Dispose();
        }
    }
}