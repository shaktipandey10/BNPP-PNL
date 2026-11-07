using Microsoft.AspNetCore.Mvc;
using PnlCapture.Api.Contracts;
using PnlCapture.Application.Imports.ImportPnlFile;

namespace PnlCapture.Api.Controllers;

[ApiController]
[Route("api/imports")]
public sealed class ImportsController(IImportPnlFileService importService) : ControllerBase
{
    private const long MaximumFileSizeBytes = 10 * 1024 * 1024;
    private const long MultipartOverheadBytes = 64 * 1024;

    /// <summary>Uploads and processes a CSV or Excel P&amp;L file.</summary>
    [HttpPost]
    [RequestSizeLimit(MaximumFileSizeBytes + MultipartOverheadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximumFileSizeBytes + MultipartOverheadBytes)]
    [ProducesResponseType<ImportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ImportResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ImportResponse>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<ImportResponse>> Upload(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ImportResponse(
                false,
                0,
                0,
                0,
                [new ImportErrorResponse(0, "A non-empty CSV or .xlsx file is required.")]));
        }

        if (file.Length > MaximumFileSizeBytes)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new ImportResponse(
                    false,
                    0,
                    0,
                    0,
                    [new ImportErrorResponse(0, "The uploaded file exceeds the 10 MB limit.")]));
        }

        await using var stream = file.OpenReadStream();
        var result = await importService.ImportAsync(stream, file.FileName, cancellationToken);
        var response = new ImportResponse(
            result.Succeeded,
            result.TotalRecords,
            result.ValidRecords,
            result.InvalidRecords,
            result.Errors.Select(error => new ImportErrorResponse(error.RowNumber, error.Message)).ToArray());

        return result.Succeeded ? Ok(response) : BadRequest(response);
    }
}
