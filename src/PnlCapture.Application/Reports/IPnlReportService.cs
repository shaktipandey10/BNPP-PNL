using PnlCapture.Domain.Entities;
using PnlCapture.Domain.Enums;

namespace PnlCapture.Application.Reports;

public interface IPnlReportService
{
    Task<IReadOnlyList<PnlRecord>> GetRecordsAsync(
        ValidationStatus status,
        string? sourceSystem,
        FeedType? feedType,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PnlReportSummary> GetSummaryAsync(
        string? sourceSystem,
        FeedType? feedType,
        CancellationToken cancellationToken);
}
