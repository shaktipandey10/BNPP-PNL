using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RabbitMQ.Client;

const string exchangeName = "pnl.exchange";
const string routingKey = "pnl.record";

var connectionUri = Environment.GetEnvironmentVariable("RABBITMQ_URI");
if (string.IsNullOrWhiteSpace(connectionUri))
{
    throw new InvalidOperationException(
        "Set RABBITMQ_URI to the CloudAMQP connection URI before running the producer.");
}

var sampleFile = Environment.GetEnvironmentVariable("PNL_PRODUCER_DATA")
    ?? Path.Combine(AppContext.BaseDirectory, "SampleData", "pnl-records.json");
if (!File.Exists(sampleFile))
{
    throw new FileNotFoundException("The producer sample JSON file was not found.", sampleFile);
}

var intervalMilliseconds = int.TryParse(
    Environment.GetEnvironmentVariable("PNL_PRODUCER_INTERVAL_MS"),
    out var configuredInterval)
    ? Math.Max(0, configuredInterval)
    : 1000;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    NumberHandling = JsonNumberHandling.Strict
};
await using var sampleStream = File.OpenRead(sampleFile);
var samples = await JsonSerializer.DeserializeAsync<List<PnlMessage>>(
    sampleStream,
    jsonOptions,
    cancellationToken: CancellationToken.None);
if (samples is null || samples.Count == 0)
{
    throw new InvalidDataException("The producer sample file must contain at least one P&L record.");
}

var factory = new ConnectionFactory
{
    Uri = new Uri(connectionUri),
    AutomaticRecoveryEnabled = true,
    NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
};

using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();
channel.ExchangeDeclare(exchangeName, ExchangeType.Direct, durable: true);
channel.ConfirmSelect();

foreach (var sample in samples)
{
    if (string.IsNullOrWhiteSpace(sample.MessageId) ||
        string.IsNullOrWhiteSpace(sample.SourceSystem))
    {
        throw new InvalidDataException("Each sample needs a MessageId and SourceSystem.");
    }

    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sample));
    var properties = channel.CreateBasicProperties();
    properties.Persistent = true;
    properties.ContentType = "application/json";
    properties.MessageId = sample.MessageId;

    channel.BasicPublish(exchangeName, routingKey, mandatory: true, properties, body);
    channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
    Console.WriteLine(
        $"Published {sample.MessageId}: {sample.SourceSystem}, account {sample.AccountNumber}, P&L {sample.PnlAmount}.");

    if (intervalMilliseconds > 0)
    {
        await Task.Delay(intervalMilliseconds);
    }
}

Console.WriteLine($"Published {samples.Count} records.");

internal sealed class PnlMessage
{
    [JsonPropertyName("messageId")]
    [JsonRequired]
    public string? MessageId { get; init; }

    [JsonPropertyName("sourceSystem")]
    [JsonRequired]
    public string? SourceSystem { get; init; }

    [JsonPropertyName("accountNumber")]
    [JsonRequired]
    public int AccountNumber { get; init; }

    [JsonPropertyName("pnlAmount")]
    [JsonRequired]
    public decimal PnlAmount { get; init; }
}
