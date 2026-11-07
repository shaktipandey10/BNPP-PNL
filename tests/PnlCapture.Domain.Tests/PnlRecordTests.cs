using PnlCapture.Domain.Entities;
using PnlCapture.Domain.Enums;
using PnlCapture.Domain.Rules;

namespace PnlCapture.Domain.Tests;

public sealed class PnlRecordTests
{
    private static readonly DateTimeOffset ProcessedAtUtc =
        new(2026, 11, 7, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithPositivePnlAmount_ClassifiesRecordAsValid()
    {
        var record = CreateRecord(1250.75m);

        Assert.Equal(ValidationStatus.Valid, record.ValidationStatus);
        Assert.True(record.IsValid);
        Assert.Null(record.ValidationReason);
    }

    [Fact]
    public void Create_WithNegativePnlAmount_ClassifiesRecordAsValid()
    {
        var record = CreateRecord(-1250.75m);

        Assert.Equal(ValidationStatus.Valid, record.ValidationStatus);
        Assert.True(record.IsValid);
        Assert.Null(record.ValidationReason);
    }

    [Fact]
    public void Create_WithZeroPnlAmount_ClassifiesRecordAsInvalid()
    {
        var record = CreateRecord(decimal.Zero);

        Assert.Equal(ValidationStatus.Invalid, record.ValidationStatus);
        Assert.False(record.IsValid);
        Assert.Equal(PnlValidationRule.ZeroAmountReason, record.ValidationReason);
    }

    [Fact]
    public void Create_KeepsSourceSystemIndependentFromFeedType()
    {
        var record = PnlRecord.Create(
            "Bloomberg",
            123456,
            10.25m,
            FeedType.File,
            ProcessedAtUtc);

        Assert.Equal("Bloomberg", record.SourceSystem);
        Assert.Equal(FeedType.File, record.FeedType);
    }

    [Fact]
    public void Create_WithZeroPnlAmount_ReturnsInvalidRecordInsteadOfDiscardingIt()
    {
        var record = CreateRecord(decimal.Zero);

        Assert.NotNull(record);
        Assert.Equal(decimal.Zero, record.PnlAmount);
        Assert.Equal(ValidationStatus.Invalid, record.ValidationStatus);
        Assert.Equal(FeedType.File, record.FeedType);
    }

    [Fact]
    public void Create_ForMessagingRequiresAndPreservesMessageId()
    {
        var record = PnlRecord.Create(
            "Reuters",
            654321,
            -1.25m,
            FeedType.Messaging,
            ProcessedAtUtc,
            "message-123");

        Assert.Equal("message-123", record.MessageId);
        Assert.Equal(FeedType.Messaging, record.FeedType);
    }

    [Fact]
    public void Create_ForMessagingWithoutMessageIdThrows()
    {
        Assert.Throws<ArgumentException>(() => PnlRecord.Create(
            "Reuters",
            654321,
            1.25m,
            FeedType.Messaging,
            ProcessedAtUtc));
    }

    private static PnlRecord CreateRecord(decimal pnlAmount)
    {
        return PnlRecord.Create(
            "InternalTradingSystem",
            123456,
            pnlAmount,
            FeedType.File,
            ProcessedAtUtc);
    }
}
