using Microsoft.Extensions.Logging;
using PnlCapture.Application.Abstractions.Notifications;
using PnlCapture.Application.Abstractions.Persistence;
using PnlCapture.Domain.Entities;
using PnlCapture.Domain.Enums;

namespace PnlCapture.Application.Processing.ProcessPnlRecord;

public sealed class PnlProcessingService(
    IPnlRecordRepository repository,
    IPnlRealtimeNotifier notifier,
    ILogger<PnlProcessingService> logger) : IPnlProcessingService
{
    public async Task<PnlProcessingResult> ProcessAsync(
        PnlRecordInput input,
        CancellationToken cancellationToken)
    {
        var record = PnlRecord.Create(
            input.SourceSystem,
            input.AccountNumber,
            input.PnlAmount,
            input.FeedType,
            DateTimeOffset.UtcNow,
            input.MessageId);

        var inserted = await repository.AddAsync(record, cancellationToken);
        if (!inserted)
        {
            return new PnlProcessingResult(null, true);
        }

        try
        {
            await notifier.NotifyRecordProcessedAsync(
                new PnlRecordNotification(
                    record.Id,
                    record.SourceSystem,
                    record.AccountNumber,
                    record.PnlAmount,
                    record.IsValid,
                    record.ValidationReason,
                    record.FeedType,
                    record.ProcessedAtUtc,
                    record.MessageId),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Failed to broadcast committed P&L record {RecordId}; report queries remain authoritative.",
                record.Id);
        }

        return new PnlProcessingResult(record, false);
    }
}
