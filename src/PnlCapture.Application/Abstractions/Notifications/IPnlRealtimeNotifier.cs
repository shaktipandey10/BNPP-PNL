using PnlCapture.Application.Processing.ProcessPnlRecord;

namespace PnlCapture.Application.Abstractions.Notifications;

public interface IPnlRealtimeNotifier
{
    Task NotifyRecordProcessedAsync(
        PnlRecordNotification notification,
        CancellationToken cancellationToken);
}
