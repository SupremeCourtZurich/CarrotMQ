using CarrotMQ.Core.Dto;

namespace Dto;

public class MyCommand : ICommand<MyCommand, MyCommand.Response, MyQueue>
{
    public string Message { get; set; }

    public class Response
    {
        public string ResponseMessage { get; set; }
    }
}