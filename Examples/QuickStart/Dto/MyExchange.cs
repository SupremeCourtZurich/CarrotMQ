using CarrotMQ.Core.EndPoints;

namespace Dto;

#region MyExchangeDefinition
public class MyExchange : ExchangeEndPoint
{
    public MyExchange() : base("my-exchange")
    {
    }
}
#endregion