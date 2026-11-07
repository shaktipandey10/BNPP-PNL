using PnlCapture.Domain.Enums;

namespace PnlCapture.Api.Contracts;

/// <summary>A persisted P&amp;L record returned by a report endpoint.</summary>
public sealed record PnlRecordResponse(
    int Id,
    string SourceSystem,
    int AccountNumber,
    decimal PnlAmount,
    bool IsValid,
    string? ValidationReason,
    FeedType FeedType,
    DateTimeOffset ProcessedAtUtc,
    string? MessageId);
