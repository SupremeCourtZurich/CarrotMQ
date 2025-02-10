using System;
using System.Threading;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Serialization;
using CarrotMQ.Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarrotMQ.Core.Configuration;

/// <summary>
/// Extension methods for <see cref="IServiceCollection" />
/// to configure CarrotMQ.Core
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add a delegate that will be called before the message is sent.
    /// Use it to alter the message or the context for all messages.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to configure</param>
    /// <param name="enrich">The delegate to be invoked</param>
    /// <returns>The configured <see cref="IServiceCollection" /></returns>
    public static IServiceCollection AddMessageEnricher(
        this IServiceCollection services,
        Action<object, Context, CancellationToken> enrich)
    {
        services.AddSingleton<IMessageEnricher>(_ => new DelegateMessageEnricher(enrich));

        return services;
    }

    /// <summary>
    /// Registers all required CarrotMQ.Core types
    /// </summary>
    public static IServiceCollection AddCarrotMqCore(this IServiceCollection services)
    {
        services.AddTransient<ICarrotClient, CarrotClient>();

        services.AddSingleton<IMessageDistributor, MessageDistributor>();
        services.AddSingleton<IResponseSender, ResponseSender>();
        services.AddSingleton<ICarrotMetricsRecorder, CarrotMetricsRecorder>();

        services.TryAddScoped<IMiddlewareProcessor, MiddlewareProcessor>();
        services.TryAddSingleton<ICarrotSerializer, DefaultCarrotSerializer>();
        services.TryAddSingleton<IDependencyInjector, DependencyInjector>();
        services.TryAddSingleton<IRoutingKeyResolver, DefaultRoutingKeyResolver>();

        return services;
    }
}