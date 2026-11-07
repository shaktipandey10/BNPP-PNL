using Microsoft.AspNetCore.Mvc;
using PnlCapture.Api.Contracts;
using PnlCapture.Application.Reports;
using PnlCapture.Domain.Enums;

namespace PnlCapture.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(IPnlReportService reportService) : ControllerBase
{
    /// <summary>Returns valid P&amp;L records with optional source and feed filters.</summary>
    [HttpGet("valid")]
    [ProducesResponseType<IReadOnlyList<PnlRecordResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PnlRecordResponse>>> GetValid(
        [FromQuery] string? sourceSystem,
        [FromQuery] FeedType? feedType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var records = await reportService.GetRecordsAsync(
            ValidationStatus.Valid,
            sourceSystem,
            feedType,
            page,
            pageSize,
            cancellationToken);

        return Ok(records.Select(ToResponse).ToArray());
    }

    /// <summary>Returns invalid P&amp;L records with optional source and feed filters.</summary>
    [HttpGet("invalid")]
    [ProducesResponseType<IReadOnlyList<PnlRecordResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PnlRecordResponse>>> GetInvalid(
        [FromQuery] string? sourceSystem,
        [FromQuery] FeedType? feedType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var records = await reportService.GetRecordsAsync(
            ValidationStatus.Invalid,
            sourceSystem,
            feedType,
            page,
            pageSize,
            cancellationToken);

        return Ok(records.Select(ToResponse).ToArray());
    }

    /// <summary>Returns total, valid, and invalid counts matching optional filters.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<PnlReportSummaryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PnlReportSummaryResponse>> GetSummary(
        [FromQuery] string? sourceSystem,
        [FromQuery] FeedType? feedType,
        CancellationToken cancellationToken)
    {
        var summary = await reportService.GetSummaryAsync(sourceSystem, feedType, cancellationToken);
        return Ok(new PnlReportSummaryResponse(
            summary.TotalCount,
            summary.ValidCount,
            summary.InvalidCount));
    }

    private static PnlRecordResponse ToResponse(Domain.Entities.PnlRecord record)
    {
        return new PnlRecordResponse(
            record.Id,
            record.SourceSystem,
            record.AccountNumber,
            record.PnlAmount,
            record.IsValid,
            record.ValidationReason,
            record.FeedType,
            record.ProcessedAtUtc,
            record.MessageId);
    }
}
