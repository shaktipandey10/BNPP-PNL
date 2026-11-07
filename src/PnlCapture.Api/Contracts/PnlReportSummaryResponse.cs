namespace PnlCapture.Api.Contracts;

/// <summary>Counts of all, valid, and invalid records matching report filters.</summary>
public sealed record PnlReportSummaryResponse(int TotalCount, int ValidCount, int InvalidCount);
