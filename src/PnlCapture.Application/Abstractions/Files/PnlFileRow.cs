namespace PnlCapture.Application.Abstractions.Files;

public sealed record PnlFileRow(string SourceSystem, int AccountNumber, decimal PnlAmount);
