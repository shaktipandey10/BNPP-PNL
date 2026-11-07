using System.Globalization;
using ClosedXML.Excel;
using PnlCapture.Application.Abstractions.Files;

namespace PnlCapture.Infrastructure.Files;

public sealed class ExcelPnlFileParser
{
    public Task<PnlFileParseResult> ParseAsync(
        Stream fileStream,
        CancellationToken cancellationToken)
    {
        var rows = new List<PnlFileRow>();
        var errors = new List<PnlFileParseError>();

        try
        {
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet is null)
            {
                return Task.FromResult(new PnlFileParseResult(
                    [],
                    [new PnlFileParseError(1, "The workbook does not contain a worksheet.")]));
            }

            var headerRow = worksheet.FirstRowUsed();
            if (headerRow is null)
            {
                return Task.FromResult(new PnlFileParseResult(
                    [],
                    [new PnlFileParseError(1, "The workbook must contain a header row.")]));
            }

            var firstColumn = headerRow.FirstCellUsed()?.Address.ColumnNumber ?? 1;
            var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                var name = headerRow.Cell(column).GetString().Trim();
                if (!string.IsNullOrEmpty(name) && !headerMap.TryAdd(name, column))
                {
                    return Task.FromResult(new PnlFileParseResult(
                        [],
                        [new PnlFileParseError(1, $"Duplicate header '{name}' is not allowed.")]));
                }
            }

            var missingHeaders = new[] { "SourceSystem", "AccountNumber", "PnLAmount" }
                .Where(header => !headerMap.ContainsKey(header))
                .ToArray();
            if (missingHeaders.Length > 0)
            {
                return Task.FromResult(new PnlFileParseResult(
                    [],
                    [new PnlFileParseError(1, $"Missing required header(s): {string.Join(", ", missingHeaders)}.")]));
            }

            foreach (var row in worksheet.RowsUsed().Where(row => row.RowNumber() > headerRow.RowNumber()))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceSystem = row.Cell(headerMap["SourceSystem"]).GetString().Trim();
                var accountValue = row.Cell(headerMap["AccountNumber"]).GetString().Trim();
                var pnlValue = row.Cell(headerMap["PnLAmount"]).GetString().Trim();
                if (string.IsNullOrWhiteSpace(sourceSystem) &&
                    string.IsNullOrWhiteSpace(accountValue) &&
                    string.IsNullOrWhiteSpace(pnlValue))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(sourceSystem))
                {
                    errors.Add(new PnlFileParseError(row.RowNumber(), "SourceSystem is required."));
                    continue;
                }

                if (!int.TryParse(accountValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var accountNumber))
                {
                    errors.Add(new PnlFileParseError(row.RowNumber(), "AccountNumber must be an integer."));
                    continue;
                }

                if (!decimal.TryParse(pnlValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var pnlAmount))
                {
                    errors.Add(new PnlFileParseError(row.RowNumber(), "PnLAmount must be a decimal using invariant format."));
                    continue;
                }

                rows.Add(new PnlFileRow(sourceSystem, accountNumber, pnlAmount));
            }
        }
        catch (IOException exception)
        {
            errors.Add(new PnlFileParseError(0, $"The Excel file could not be read: {exception.Message}"));
        }
        catch (InvalidDataException exception)
        {
            errors.Add(new PnlFileParseError(0, $"The Excel file is invalid: {exception.Message}"));
        }

        return Task.FromResult(new PnlFileParseResult(rows, errors));
    }
}
