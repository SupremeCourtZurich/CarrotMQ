using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.Test.Protocol;

[TestClass]
public class PublishFailedExceptionTests
{
    [TestMethod]
    public void RetryLimitExceededException_IsAPublishFailedException()
    {
        var exception = new RetryLimitExceededException();

        Assert.IsInstanceOfType<PublishFailedException>(exception);
    }

    [TestMethod]
    public void RetryLimitExceededException_ExistingConstructors_HaveAnUnknownReason()
    {
        var inner = new InvalidOperationException("inner");

        var exceptions = new[]
        {
            new RetryLimitExceededException(),
            new RetryLimitExceededException("message"),
            new RetryLimitExceededException("message", inner)
        };

        foreach (var exception in exceptions)
        {
            Assert.AreEqual(PublishFailureReason.Unknown, exception.Reason);
        }
    }

    [TestMethod]
    public void RetryLimitExceededException_WithReason_KeepsReasonMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new RetryLimitExceededException("refused", PublishFailureReason.DestinationNotFound, inner);

        Assert.AreEqual(PublishFailureReason.DestinationNotFound, exception.Reason);
        Assert.IsTrue(string.Equals("refused", exception.Message, StringComparison.Ordinal), exception.Message);
        Assert.AreSame(inner, exception.InnerException);
    }

    [TestMethod]
    public void PublishFailedException_WithoutReason_HasAnUnknownReason()
    {
        var exception = new PublishFailedException("message");

        Assert.AreEqual(PublishFailureReason.Unknown, exception.Reason);
    }
}