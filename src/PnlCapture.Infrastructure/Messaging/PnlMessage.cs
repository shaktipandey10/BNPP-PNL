using System.Text.Json;
using System.Text.Json.Serialization;

namespace PnlCapture.Infrastructure.Messaging;

internal sealed class PnlMessage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict
    };

    [JsonPropertyName("messageId")]
    [JsonRequired]
    public string MessageId { get; init; } = string.Empty;

    [JsonPropertyName("sourceSystem")]
    [JsonRequired]
    public string SourceSystem { get; init; } = string.Empty;

    [JsonPropertyName("accountNumber")]
    [JsonRequired]
    public int AccountNumber { get; init; }

    [JsonPropertyName("pnlAmount")]
    [JsonRequired]
    public decimal PnlAmount { get; init; }

    public static PnlMessage Deserialize(ReadOnlySpan<byte> body)
    {
        var message = JsonSerializer.Deserialize<PnlMessage>(body, JsonOptions)
            ?? throw new InvalidDataException("The message body must contain a P&L record.");

        if (string.IsNullOrWhiteSpace(message.MessageId) ||
            string.IsNullOrWhiteSpace(message.SourceSystem))
        {
            throw new InvalidDataException("MessageId and SourceSystem are required.");
        }

        return message;
    }
}
