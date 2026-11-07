namespace PnlCapture.Application.Processing.ProcessPnlRecord;

public interface IPnlProcessingService
{
    Task<PnlProcessingResult> ProcessAsync(
        PnlRecordInput input,
        CancellationToken cancellationToken);
}
