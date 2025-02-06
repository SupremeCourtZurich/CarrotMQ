using System.Threading.Tasks;

namespace CarrotMQ.Core;

/// 
public interface ICarrotConsumerManager
{
    /// <summary>
    /// Starts all consumers.
    /// Note: This method may throw exceptions from underlying system or library code (serializers or transport specific
    /// implementations).
    /// </summary>
    Task StartConsumingAsync();

    /// <summary>
    /// Stops all consumers.
    /// Note: This method may throw exceptions from underlying system or library code (serializers or transport specific
    /// implementations).
    /// </summary>
    Task StopConsumingAsync();
}