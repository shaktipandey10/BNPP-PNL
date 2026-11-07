using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PnlCapture.Domain.Entities;
using PnlCapture.Domain.Enums;
using PnlCapture.Infrastructure.Persistence;

namespace PnlCapture.Infrastructure.Tests;

public sealed class PnlRecordRepositoryTests
{
    [Fact]
    public async Task AddAsync_PersistsInvalidRecordAndIgnoresDuplicateMessageId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PnlDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new PnlDbContext(options);
        await database.Database.EnsureCreatedAsync();
        var repository = new PnlRecordRepository(database);
        var timestamp = DateTimeOffset.UtcNow;
        var record = PnlRecord.Create(
            "Reuters",
            9876,
            0m,
            FeedType.Messaging,
            timestamp,
            "stable-message-id");

        Assert.True(await repository.AddAsync(record, CancellationToken.None));

        var duplicate = PnlRecord.Create(
            "Reuters",
            9876,
            0m,
            FeedType.Messaging,
            timestamp,
            "stable-message-id");
        Assert.False(await repository.AddAsync(duplicate, CancellationToken.None));

        var persisted = await database.PnlRecords.SingleAsync();
        Assert.False(persisted.IsValid);
        Assert.Equal(0m, persisted.PnlAmount);
        Assert.Equal("stable-message-id", persisted.MessageId);
    }
}
