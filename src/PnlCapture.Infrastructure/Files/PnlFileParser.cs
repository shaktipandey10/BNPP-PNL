using PnlCapture.Application.Abstractions.Files;

namespace PnlCapture.Infrastructure.Files;

public sealed class PnlFileParser(
    CsvPnlFileParser csvParser,
    ExcelPnlFileParser excelParser) : IPnlFileParser
{
    public Task<PnlFileParseResult> ParseAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".csv" => csvParser.ParseAsync(fileStream, cancellationToken),
            ".xlsx" => excelParser.ParseAsync(fileStream, cancellationToken),
            _ => Task.FromResult(new PnlFileParseResult(
                [],
                [new PnlFileParseError(0, "Only CSV and .xlsx files are supported.")]))
        };
    }
}
