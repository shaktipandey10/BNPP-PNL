using Microsoft.Extensions.Logging;
using Moq;
using PnlCapture.Application.Abstractions.Notifications;
using PnlCapture.Application.Abstractions.Persistence;
using PnlCapture.Application.Processing.ProcessPnlRecord;
using PnlCapture.Domain.Enums;

namespace PnlCapture.Application.Tests;

public sealed class PnlProcessingServiceTests
{
    [Theory]
    [InlineData(12.5, true)]
    [InlineData(-12.5, true)]
    [InlineData(0, false)]
    public async Task ProcessAsync_PersistsAndNotifiesValidAndInvalidRecords(decimal amount, bool expectedValid)
    {
        var repository = new Mock<IPnlRecordRepository>();
        var notifier = new Mock<IPnlRealtimeNotifier>();
        repository
            .Setup(item => item.AddAsync(
                It.IsAny<Domain.Entities.PnlRecord>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        notifier
            .Setup(item => item.NotifyRecordProcessedAsync(
                It.IsAny<PnlRecordNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(repository, notifier);
        var result = await service.ProcessAsync(
            new PnlRecordInput("Bloomberg", 123, amount, FeedType.File),
            CancellationToken.None);

        Assert.NotNull(result.Record);
        Assert.Equal(expectedValid, result.Record.IsValid);
        Assert.Equal(amount, result.Record.PnlAmount);
        Assert.False(result.IsDuplicate);
        repository.Verify(item => item.AddAsync(
            It.Is<Domain.Entities.PnlRecord>(record => record.PnlAmount == amount),
            It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(item => item.NotifyRecordProcessedAsync(
            It.Is<PnlRecordNotification>(notification => notification.IsValid == expectedValid),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_DuplicateMessageDoesNotBroadcastAgain()
    {
        var repository = new Mock<IPnlRecordRepository>();
        var notifier = new Mock<IPnlRealtimeNotifier>();
        repository
            .Setup(item => item.AddAsync(
                It.IsAny<Domain.Entities.PnlRecord>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService(repository, notifier);
        var result = await service.ProcessAsync(
            new PnlRecordInput("Reuters", 456, 1.25m, FeedType.Messaging, "message-1"),
            CancellationToken.None);

        Assert.True(result.IsDuplicate);
        Assert.Null(result.Record);
        notifier.Verify(
            item => item.NotifyRecordProcessedAsync(
                It.IsAny<PnlRecordNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static PnlProcessingService CreateService(
        Mock<IPnlRecordRepository> repository,
        Mock<IPnlRealtimeNotifier> notifier)
    {
        return new PnlProcessingService(
            repository.Object,
            notifier.Object,
            Mock.Of<ILogger<PnlProcessingService>>());
    }
}
