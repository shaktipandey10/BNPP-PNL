namespace PnlCapture.Application.Abstractions.Files;

public sealed record PnlFileParseError(int RowNumber, string Message);
