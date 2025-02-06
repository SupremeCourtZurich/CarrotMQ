using CarrotMQ.Core.Configuration;

namespace CarrotMQ.RabbitMQ.Configuration.Exchanges;

/// 
public abstract class Exchange
{
    internal BindingCollection BindingCollection;

    /// 
    protected Exchange(string name, BindingCollection bindingCollection)
    {
        Name = name;
        BindingCollection = bindingCollection;
    }

    /// 
    internal string Name { get; }
}