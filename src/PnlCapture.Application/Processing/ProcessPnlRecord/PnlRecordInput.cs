using PnlCapture.Domain.Enums;

namespace PnlCapture.Application.Processing.ProcessPnlRecord;

public sealed record PnlRecordInput(
    string SourceSystem,
    int AccountNumber,
    decimal PnlAmount,
    FeedType FeedType,
    string? MessageId = null);
