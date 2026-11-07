using PnlCapture.Application.Abstractions.Files;
using PnlCapture.Application.Processing.ProcessPnlRecord;
using PnlCapture.Domain.Enums;

namespace PnlCapture.Application.Imports.ImportPnlFile;

public sealed class ImportPnlFileService(
    IPnlFileParser fileParser,
    IPnlProcessingService processingService) : IImportPnlFileService
{
    public async Task<ImportPnlFileResult> ImportAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        var parsed = await fileParser.ParseAsync(fileStream, fileName, cancellationToken);
        if (parsed.Errors.Count > 0)
        {
            return new ImportPnlFileResult(false, 0, 0, 0, parsed.Errors);
        }

        var validRecords = 0;
        var invalidRecords = 0;

        foreach (var row in parsed.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await processingService.ProcessAsync(
                new PnlRecordInput(
                    row.SourceSystem,
                    row.AccountNumber,
                    row.PnlAmount,
                    FeedType.File),
                cancellationToken);

            if (result.Record?.IsValid == true)
            {
                validRecords++;
            }
            else if (result.Record is not null)
            {
                invalidRecords++;
            }
        }

        return new ImportPnlFileResult(
            true,
            validRecords + invalidRecords,
            validRecords,
            invalidRecords,
            []);
    }
}
