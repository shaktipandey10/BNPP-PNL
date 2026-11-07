using Moq;
using PnlCapture.Application.Abstractions.Files;
using PnlCapture.Application.Imports.ImportPnlFile;
using PnlCapture.Application.Processing.ProcessPnlRecord;
using PnlCapture.Domain.Enums;

namespace PnlCapture.Application.Tests;

public sealed class ImportPnlFileServiceTests
{
    [Fact]
    public async Task ImportAsync_StructuralErrorsPreventAllRecordProcessing()
    {
        var parser = new Mock<IPnlFileParser>();
        var processor = new Mock<IPnlProcessingService>();
        parser
            .Setup(item => item.ParseAsync(
                It.IsAny<Stream>(),
                "broken.csv",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PnlFileParseResult(
                [],
                [new PnlFileParseError(2, "AccountNumber must be an integer.")]));

        var service = new ImportPnlFileService(parser.Object, processor.Object);
        await using var stream = new MemoryStream();

        var result = await service.ImportAsync(stream, "broken.csv", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Single(result.Errors);
        processor.Verify(
            item => item.ProcessAsync(It.IsAny<PnlRecordInput>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportAsync_ProcessesParsedRowsSequentiallyIncludingZeroPnl()
    {
        var parser = new Mock<IPnlFileParser>();
        var processor = new Mock<IPnlProcessingService>();
        parser
            .Setup(item => item.ParseAsync(
                It.IsAny<Stream>(),
                "records.csv",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PnlFileParseResult(
                [
                    new PnlFileRow("Bloomberg", 1, 25m),
                    new PnlFileRow("Reuters", 2, 0m)
                ],
                []));
        processor
            .SetupSequence(item => item.ProcessAsync(
                It.IsAny<PnlRecordInput>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PnlProcessingResult(
                Domain.Entities.PnlRecord.Create("Bloomberg", 1, 25m, FeedType.File, DateTimeOffset.UtcNow),
                false))
            .ReturnsAsync(new PnlProcessingResult(
                Domain.Entities.PnlRecord.Create("Reuters", 2, 0m, FeedType.File, DateTimeOffset.UtcNow),
                false));

        var service = new ImportPnlFileService(parser.Object, processor.Object);
        await using var stream = new MemoryStream();

        var result = await service.ImportAsync(stream, "records.csv", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(1, result.ValidRecords);
        Assert.Equal(1, result.InvalidRecords);
        processor.Verify(item => item.ProcessAsync(
            It.IsAny<PnlRecordInput>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
