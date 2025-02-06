using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Represents a message to be published with publisher confirms.
/// </summary>
internal sealed class PublisherConfirmMessage
{
    public PublisherConfirmMessage(
        string messagePayload,
        CarrotHeader messageHeader,
        TaskCompletionSource<bool> taskCompletionSource,
        CancellationToken cancellationToken)
    {
        MessagePayload = messagePayload;
        MessageHeader = messageHeader;
        CancellationToken = cancellationToken;
        CompletionSource = taskCompletionSource;

        Payload = Encoding.UTF8.GetBytes(messagePayload);
    }

    /// <summary>
    /// Number of republish attempts for the message.
    /// </summary>
    public int RepublishCount { get; set; } = 0;

    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>
    /// Byte array representing the payload of the message.
    /// </summary>
    public byte[] Payload { get; }

    public string MessagePayload { get; }

    public CarrotHeader MessageHeader { get; }

    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Task completion source used to track the completion status of the publishing operation.
    /// </summary>
    public TaskCompletionSource<bool> CompletionSource { get; }

    public ulong SeqNo { get; set; }
}