using PnlCapture.Domain.Enums;
using PnlCapture.Domain.Rules;

namespace PnlCapture.Domain.Entities;

public sealed class PnlRecord
{
    private bool _isValid;

    private PnlRecord()
    {
        SourceSystem = string.Empty;
    }

    private PnlRecord(
        string sourceSystem,
        int accountNumber,
        decimal pnlAmount,
        FeedType feedType,
        PnlValidationResult validation,
        DateTimeOffset processedAtUtc,
        string? messageId)
    {
        SourceSystem = sourceSystem;
        AccountNumber = accountNumber;
        PnlAmount = pnlAmount;
        FeedType = feedType;
        _isValid = validation.Status == ValidationStatus.Valid;
        ValidationReason = validation.Reason;
        ProcessedAtUtc = processedAtUtc;
        MessageId = messageId;
    }

    public int Id { get; private set; }

    public string SourceSystem { get; private set; }

    public int AccountNumber { get; private set; }

    public decimal PnlAmount { get; private set; }

    public FeedType FeedType { get; private set; }

    public ValidationStatus ValidationStatus => _isValid ? ValidationStatus.Valid : ValidationStatus.Invalid;

    public bool IsValid => _isValid;

    public string? ValidationReason { get; private set; }

    public DateTimeOffset ProcessedAtUtc { get; private set; }

    public string? MessageId { get; private set; }

    public static PnlRecord Create(
        string sourceSystem,
        int accountNumber,
        decimal pnlAmount,
        FeedType feedType,
        DateTimeOffset processedAtUtc,
        string? messageId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);

        if (!Enum.IsDefined(feedType))
        {
            throw new ArgumentOutOfRangeException(nameof(feedType), feedType, "Unsupported feed type.");
        }

        if (feedType == FeedType.Messaging && string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("Messaging records require a message ID.", nameof(messageId));
        }

        if (feedType == FeedType.File && messageId is not null)
        {
            throw new ArgumentException("File records cannot have a message ID.", nameof(messageId));
        }

        var validation = PnlValidationRule.Evaluate(pnlAmount);

        return new PnlRecord(
            sourceSystem,
            accountNumber,
            pnlAmount,
            feedType,
            validation,
            processedAtUtc.ToUniversalTime(),
            messageId);
    }
}
