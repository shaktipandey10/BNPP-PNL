namespace PnlCapture.Application.Imports.ImportPnlFile;

public interface IImportPnlFileService
{
    Task<ImportPnlFileResult> ImportAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken);
}
