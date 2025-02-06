using System;
using CarrotMQ.Core;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarrotMQ.RabbitMQ.Configuration;

/// <summary>
/// Extension methods for registering CarrotMQ-related services in the <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtension
{
    ///
    public static IServiceCollection AddCarrotMqRabbitMq(this IServiceCollection services, Action<CarrotConfigurationBuilder>? configure = null)
    {
        CarrotConfigurationBuilder builder = new(services);

        configure?.Invoke(builder);

        builder.Build();

        services.AddCarrotMqCore();

        services.AddSingleton<ICarrotConsumerManager, CarrotConsumerManager>();
        services.AddSingleton<ITransport, RabbitTransport>();
        services.TryAddSingleton<IProtocolSerializer, ProtocolSerializer>();

        return services;
    }
}