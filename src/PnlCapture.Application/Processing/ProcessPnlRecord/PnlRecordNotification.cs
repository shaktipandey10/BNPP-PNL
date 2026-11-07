using PnlCapture.Domain.Enums;

namespace PnlCapture.Application.Processing.ProcessPnlRecord;

public sealed record PnlRecordNotification(
    int Id,
    string SourceSystem,
    int AccountNumber,
    decimal PnlAmount,
    bool IsValid,
    string? ValidationReason,
    FeedType FeedType,
    DateTimeOffset ProcessedAtUtc,
    string? MessageId);
