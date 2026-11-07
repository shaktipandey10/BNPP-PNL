namespace PnlCapture.Api.Contracts;

/// <summary>Result of uploading a P&amp;L batch file.</summary>
public sealed record ImportResponse(
    bool Succeeded,
    int TotalRecords,
    int ValidRecords,
    int InvalidRecords,
    IReadOnlyList<ImportErrorResponse> Errors);

/// <summary>A structural or parsing issue identified in an uploaded file.</summary>
public sealed record ImportErrorResponse(int RowNumber, string Message);
