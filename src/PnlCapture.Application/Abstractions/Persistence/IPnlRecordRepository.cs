using PnlCapture.Domain.Entities;
using PnlCapture.Domain.Enums;

namespace PnlCapture.Application.Abstractions.Persistence;

public interface IPnlRecordRepository
{
    Task<bool> AddAsync(PnlRecord record, CancellationToken cancellationToken);

    Task<IReadOnlyList<PnlRecord>> GetAsync(
        ValidationStatus? status,
        string? sourceSystem,
        FeedType? feedType,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<(int TotalCount, int ValidCount, int InvalidCount)> GetSummaryAsync(
        string? sourceSystem,
        FeedType? feedType,
        CancellationToken cancellationToken);
}
