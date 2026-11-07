namespace PnlCapture.Application.Abstractions.Files;

public sealed record PnlFileParseResult(
    IReadOnlyList<PnlFileRow> Rows,
    IReadOnlyList<PnlFileParseError> Errors);
