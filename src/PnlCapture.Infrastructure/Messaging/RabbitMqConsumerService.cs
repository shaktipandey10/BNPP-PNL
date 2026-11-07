using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PnlCapture.Application.Processing.ProcessPnlRecord;
using PnlCapture.Domain.Enums;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PnlCapture.Infrastructure.Messaging;

public sealed class RabbitMqConsumerService(
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqConsumerService> logger) : BackgroundService
{
    private const string RetryCountHeader = "x-retry-count";
    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionUri))
        {
            logger.LogWarning("RabbitMQ consumer is disabled because RabbitMq:ConnectionUri is not configured.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            IConnection? connection = null;
            IModel? channel = null;

            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(_options.ConnectionUri),
                    DispatchConsumersAsync = true,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
                };

                connection = factory.CreateConnection();
                channel = connection.CreateModel();
                DeclareTopology(channel);
                channel.BasicQos(0, 1, false);
                channel.ConfirmSelect();

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, delivery) =>
                    await ProcessDeliveryAsync(channel, delivery, stoppingToken);

                channel.BasicConsume(_options.QueueName, autoAck: false, consumer);
                logger.LogInformation("RabbitMQ consumer started for queue {QueueName}.", _options.QueueName);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "RabbitMQ consumer disconnected. Retrying in five seconds.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
            finally
            {
                channel?.Dispose();
                connection?.Dispose();
            }
        }
    }

    private async Task ProcessDeliveryAsync(
        IModel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        PnlMessage message;
        try
        {
            message = PnlMessage.Deserialize(delivery.Body.Span);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            logger.LogWarning(exception, "Malformed RabbitMQ message routed to the dead-letter queue.");
            RouteToQueueOrRequeue(
                channel,
                delivery,
                _options.DeadLetterQueueName,
                delivery.BasicProperties,
                "malformed message");
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IPnlProcessingService>();
            var result = await processor.ProcessAsync(
                new PnlRecordInput(
                    message.SourceSystem,
                    message.AccountNumber,
                    message.PnlAmount,
                    FeedType.Messaging,
                    message.MessageId),
                cancellationToken);

            if (result.IsDuplicate)
            {
                logger.LogInformation("Acknowledging duplicate P&L message {MessageId}.", message.MessageId);
            }

            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (ArgumentException exception)
        {
            logger.LogWarning(exception, "Invalid P&L message {MessageId} routed to the dead-letter queue.", message.MessageId);
            RouteToQueueOrRequeue(
                channel,
                delivery,
                _options.DeadLetterQueueName,
                delivery.BasicProperties,
                message.MessageId!);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (channel.IsOpen)
            {
                channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: true);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process P&L message {MessageId}.", message.MessageId);
            try
            {
                var retryCount = ReadRetryCount(delivery.BasicProperties);
                var targetQueue = GetFailureQueueName(retryCount, _options);

                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";
                properties.MessageId = message.MessageId;
                properties.Headers = new Dictionary<string, object>
                {
                    [RetryCountHeader] = retryCount + 1
                };

                RouteToQueueOrRequeue(channel, delivery, targetQueue, properties, message.MessageId!);
            }
            catch (Exception routeException)
            {
                logger.LogError(
                    routeException,
                    "Could not prepare failure routing for message {MessageId}; requeueing original delivery.",
                    message.MessageId);
                if (channel.IsOpen)
                {
                    channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: true);
                }
            }
        }
    }

    private void RouteToQueueOrRequeue(
        IModel channel,
        BasicDeliverEventArgs delivery,
        string targetQueue,
        IBasicProperties properties,
        string messageId)
    {
        try
        {
            PublishToQueue(channel, targetQueue, delivery.Body.ToArray(), properties);
            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Could not route P&L message {MessageId} to {QueueName}; requeueing original delivery.",
                messageId,
                targetQueue);
            if (channel.IsOpen)
            {
                channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: true);
            }
        }
    }

    private void DeclareTopology(IModel channel)
    {
        channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Direct, durable: true);
        channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(_options.QueueName, _options.ExchangeName, "pnl.record");

        channel.QueueDeclare(
            _options.RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-message-ttl"] = _options.RetryDelayMilliseconds,
                ["x-dead-letter-exchange"] = _options.ExchangeName,
                ["x-dead-letter-routing-key"] = "pnl.record"
            });

        channel.QueueDeclare(_options.DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false);
    }

    private static int ReadRetryCount(IBasicProperties properties)
    {
        if (properties.Headers is null ||
            !properties.Headers.TryGetValue(RetryCountHeader, out var value))
        {
            return 0;
        }

        return value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number => checked((int)number),
            byte[] bytes when int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out var number) => number,
            _ => 0
        };
    }

    internal static string GetFailureQueueName(int retryCount, RabbitMqOptions options)
    {
        return retryCount < options.MaxRetryCount
            ? options.RetryQueueName
            : options.DeadLetterQueueName;
    }

    private static void PublishToQueue(
        IModel channel,
        string queueName,
        byte[] body,
        IBasicProperties sourceProperties)
    {
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = sourceProperties.ContentType ?? "application/json";
        properties.MessageId = sourceProperties.MessageId;
        properties.Headers = sourceProperties.Headers is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(sourceProperties.Headers);

        string? returnedMessageId = null;
        string? returnReason = null;
        EventHandler<BasicReturnEventArgs> onReturned = (_, args) =>
        {
            if (args.BasicProperties.MessageId == properties.MessageId)
            {
                Volatile.Write(ref returnedMessageId, args.BasicProperties.MessageId);
                Volatile.Write(ref returnReason, $"{args.ReplyCode} {args.ReplyText}");
            }
        };

        channel.BasicReturn += onReturned;
        try
        {
            channel.BasicPublish(string.Empty, queueName, mandatory: true, properties, body);
            channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));

            if (Volatile.Read(ref returnedMessageId) is not null)
            {
                throw new InvalidOperationException(
                    $"RabbitMQ could not route message to queue '{queueName}': " +
                    $"{Volatile.Read(ref returnReason) ?? "unroutable"}.");
            }
        }
        finally
        {
            channel.BasicReturn -= onReturned;
        }
    }
}
