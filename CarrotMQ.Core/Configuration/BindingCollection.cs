using System;
using System.Collections.Generic;
using System.Linq;
using CarrotMQ.Core.MessageProcessing;

namespace CarrotMQ.Core.Configuration;

/// 
public sealed class BindingCollection
{
    private readonly List<BindingConfiguration> _bindings = [];

    /// <summary>
    /// Adds a binding between exchanges and queues.
    /// </summary>
    /// <param name="bindingConfiguration">The binding to add.</param>
    public void AddBinding(BindingConfiguration bindingConfiguration)
    {
        _bindings.Add(bindingConfiguration);
    }

    ///
    public IList<BindingConfiguration> GetBindingsForQueue(string queueName)
    {
        return _bindings
            .Where(b => b.Queue.Equals(queueName, StringComparison.InvariantCultureIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Resolve the <see cref="BindingConfiguration.RoutingKey" /> for all bindings
    /// </summary>
    public void ResolveRoutingKeys(IRoutingKeyResolver routingKeyResolver)
    {
        _bindings.ForEach(b => b.ResolveRoutingKey(routingKeyResolver));
    }
}