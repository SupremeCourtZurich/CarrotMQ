using CarrotMQ.Core.Dto;

namespace Dto;

public class MyQuery : IQuery<MyQuery, MyQuery.Response, MyQueue>
{
    public string Message { get; set; }
    public class Response
    {
        public string ResponseMessage { get; set; }
    }
}