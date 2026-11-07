namespace PnlCapture.Application.Abstractions.Files;

public interface IPnlFileParser
{
    Task<PnlFileParseResult> ParseAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken);
}
