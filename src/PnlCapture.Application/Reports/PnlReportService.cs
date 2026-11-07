using PnlCapture.Application.Abstractions.Persistence;
using PnlCapture.Domain.Entities;
using PnlCapture.Domain.Enums;

namespace PnlCapture.Application.Reports;

public sealed class PnlReportService(IPnlRecordRepository repository) : IPnlReportService
{
    public Task<IReadOnlyList<PnlRecord>> GetRecordsAsync(
        ValidationStatus status,
        string? sourceSystem,
        FeedType? feedType,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");
        }

        if (pageSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 500.");
        }

        var skip = checked((page - 1) * pageSize);
        return repository.GetAsync(
            status,
            NormalizeSource(sourceSystem),
            feedType,
            skip,
            pageSize,
            cancellationToken);
    }

    public async Task<PnlReportSummary> GetSummaryAsync(
        string? sourceSystem,
        FeedType? feedType,
        CancellationToken cancellationToken)
    {
        var counts = await repository.GetSummaryAsync(
            NormalizeSource(sourceSystem),
            feedType,
            cancellationToken);

        return new PnlReportSummary(counts.TotalCount, counts.ValidCount, counts.InvalidCount);
    }

    private static string? NormalizeSource(string? sourceSystem)
    {
        return string.IsNullOrWhiteSpace(sourceSystem) ? null : sourceSystem.Trim();
    }
}
