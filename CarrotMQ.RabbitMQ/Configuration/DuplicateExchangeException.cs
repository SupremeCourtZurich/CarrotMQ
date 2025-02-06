using System;

namespace CarrotMQ.RabbitMQ.Configuration;

/// 
public class DuplicateExchangeException : Exception
{
    /// 
    public DuplicateExchangeException(string exchange, Exception? innerException = null) : base(
        $"The exchange '{exchange}' was already registered",
        innerException)
    {
    }
}