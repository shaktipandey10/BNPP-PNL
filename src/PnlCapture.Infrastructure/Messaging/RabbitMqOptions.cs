namespace PnlCapture.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string? ConnectionUri { get; set; }

    public string ExchangeName { get; set; } = "pnl.exchange";

    public string QueueName { get; set; } = "pnl.records";

    public string RetryQueueName { get; set; } = "pnl.retry";

    public string DeadLetterQueueName { get; set; } = "pnl.dead";

    public int RetryDelayMilliseconds { get; set; } = 5000;

    public int MaxRetryCount { get; set; } = 3;
}
