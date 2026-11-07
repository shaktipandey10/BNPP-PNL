using PnlCapture.Application.Abstractions.Files;

namespace PnlCapture.Application.Imports.ImportPnlFile;

public sealed record ImportPnlFileResult(
    bool Succeeded,
    int TotalRecords,
    int ValidRecords,
    int InvalidRecords,
    IReadOnlyList<PnlFileParseError> Errors);
