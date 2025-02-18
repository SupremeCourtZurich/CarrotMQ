using CarrotMQ.Core.EndPoints;

namespace Dto;

public class MyExchange : ExchangeEndPoint
{
    public MyExchange() : base("MyExchange")
    {
    }
}