using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.Test.Protocol;

[TestClass]
public class PublishFailedExceptionTests
{
    [TestMethod]
    public void RetryLimitExceededException_IsAPublishFailedException()
    {
        // Act
        var exception = new RetryLimitExceededException();

        // Assert
        Assert.IsInstanceOfType<PublishFailedException>(exception);
    }

    [TestMethod]
    public void RetryLimitExceededException_ExistingConstructors_HaveAnUnknownReason()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var exceptions = new[]
        {
            new RetryLimitExceededException(),
            new RetryLimitExceededException("message"),
            new RetryLimitExceededException("message", inner)
        };

        // Assert
        foreach (var exception in exceptions)
        {
            Assert.AreEqual(PublishFailureReason.Unknown, exception.Reason);
        }
    }

    [TestMethod]
    public void RetryLimitExceededException_WithReason_KeepsReasonMessageAndInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var exception = new RetryLimitExceededException("no confirm", PublishFailureReason.Unreachable, inner);

        // Assert
        Assert.AreEqual(PublishFailureReason.Unreachable, exception.Reason);
        Assert.AreEqual("no confirm", exception.Message, StringComparer.Ordinal);
        Assert.AreSame(inner, exception.InnerException);
    }

    [TestMethod]
    [DataRow(PublishFailureReason.Unknown, 0)]
    [DataRow(PublishFailureReason.Unreachable, 1)]
    public void PublishFailureReason_HasAStableNumericValue(PublishFailureReason reason, int value)
    {
        // Act
        var actual = (int)reason;

        // Assert
        Assert.AreEqual(value, actual);
    }

    [TestMethod]
    public void PublishFailedException_WithoutReason_HasAnUnknownReason()
    {
        // Act
        var exception = new PublishFailedException("message");

        // Assert
        Assert.AreEqual(PublishFailureReason.Unknown, exception.Reason);
    }
}
