using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PnlCapture.Application.Abstractions.Persistence;
using PnlCapture.Domain.Entities;
using PnlCapture.Domain.Enums;

namespace PnlCapture.Infrastructure.Persistence;

public sealed class PnlRecordRepository(PnlDbContext dbContext) : IPnlRecordRepository
{
    public async Task<bool> AddAsync(PnlRecord record, CancellationToken cancellationToken)
    {
        dbContext.PnlRecords.Add(record);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (record.MessageId is not null && IsUniqueConstraintViolation(exception))
        {
            dbContext.Entry(record).State = EntityState.Detached;
            if (await dbContext.PnlRecords.AnyAsync(item => item.MessageId == record.MessageId, cancellationToken))
            {
                return false;
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<PnlRecord>> GetAsync(
        ValidationStatus? status,
        string? sourceSystem,
        FeedType? feedType,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        IQueryable<PnlRecord> query = dbContext.PnlRecords.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(record => record.IsValid == (status == ValidationStatus.Valid));
        }

        if (sourceSystem is not null)
        {
            query = query.Where(record => record.SourceSystem == sourceSystem);
        }

        if (feedType.HasValue)
        {
            query = query.Where(record => record.FeedType == feedType);
        }

        return await query
            .OrderByDescending(record => record.ProcessedAtUtc)
            .ThenByDescending(record => record.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<(int TotalCount, int ValidCount, int InvalidCount)> GetSummaryAsync(
        string? sourceSystem,
        FeedType? feedType,
        CancellationToken cancellationToken)
    {
        var query = dbContext.PnlRecords.AsNoTracking().AsQueryable();

        if (sourceSystem is not null)
        {
            query = query.Where(record => record.SourceSystem == sourceSystem);
        }

        if (feedType.HasValue)
        {
            query = query.Where(record => record.FeedType == feedType);
        }

        var total = await query.CountAsync(cancellationToken);
        var valid = await query.CountAsync(record => record.IsValid, cancellationToken);
        return (total, valid, total - valid);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException { SqliteErrorCode: 19 };
    }
}
