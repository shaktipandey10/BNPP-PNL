using PnlCapture.Infrastructure.Messaging;

namespace PnlCapture.Infrastructure.Tests;

public sealed class RabbitMqConsumerPolicyTests
{
    [Theory]
    [InlineData(0, "pnl.retry")]
    [InlineData(1, "pnl.retry")]
    [InlineData(2, "pnl.dead")]
    [InlineData(3, "pnl.dead")]
    public void GetFailureQueueName_UsesBoundedRetriesThenDeadLetters(
        int retryCount,
        string expectedQueue)
    {
        var options = new RabbitMqOptions
        {
            RetryQueueName = "pnl.retry",
            DeadLetterQueueName = "pnl.dead",
            MaxRetryCount = 2
        };

        var queue = RabbitMqConsumerService.GetFailureQueueName(retryCount, options);

        Assert.Equal(expectedQueue, queue);
    }
}
