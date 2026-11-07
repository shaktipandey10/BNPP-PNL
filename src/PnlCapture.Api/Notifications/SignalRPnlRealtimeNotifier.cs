using Microsoft.AspNetCore.SignalR;
using PnlCapture.Api.Hubs;
using PnlCapture.Application.Abstractions.Notifications;
using PnlCapture.Application.Processing.ProcessPnlRecord;

namespace PnlCapture.Api.Notifications;

public sealed class SignalRPnlRealtimeNotifier(IHubContext<PnlHub> hubContext) : IPnlRealtimeNotifier
{
    public Task NotifyRecordProcessedAsync(
        PnlRecordNotification notification,
        CancellationToken cancellationToken)
    {
        return hubContext.Clients.All.SendAsync(
            "PnlRecordProcessed",
            notification,
            cancellationToken);
    }
}
