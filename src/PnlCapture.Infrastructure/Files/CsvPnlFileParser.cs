using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using PnlCapture.Application.Abstractions.Files;

namespace PnlCapture.Infrastructure.Files;

public sealed class CsvPnlFileParser
{
    public async Task<PnlFileParseResult> ParseAsync(
        Stream fileStream,
        CancellationToken cancellationToken)
    {
        var rows = new List<PnlFileRow>();
        var errors = new List<PnlFileParseError>();

        try
        {
            using var reader = new StreamReader(fileStream, leaveOpen: true);
            var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                DetectColumnCountChanges = true
            };
            using var csv = new CsvReader(reader, configuration);

            if (!await csv.ReadAsync())
            {
                return new PnlFileParseResult([], [new PnlFileParseError(1, "The file is empty.")]);
            }

            csv.ReadHeader();
            var headers = csv.HeaderRecord;
            if (headers is null)
            {
                return new PnlFileParseResult([], [new PnlFileParseError(1, "A header row is required.")]);
            }

            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Length; index++)
            {
                var name = headers[index].Trim();
                if (!string.IsNullOrEmpty(name) && !headerMap.TryAdd(name, index))
                {
                    return new PnlFileParseResult(
                        [],
                        [new PnlFileParseError(1, $"Duplicate header '{name}' is not allowed.")]);
                }
            }

            var missingHeaders = new[] { "SourceSystem", "AccountNumber", "PnLAmount" }
                .Where(header => !headerMap.ContainsKey(header))
                .ToArray();
            if (missingHeaders.Length > 0)
            {
                return new PnlFileParseResult(
                    [],
                    [new PnlFileParseError(1, $"Missing required header(s): {string.Join(", ", missingHeaders)}.")]);
            }

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rowNumber = csv.Parser.Row;
                var fields = csv.Parser.Record ?? [];
                if (fields.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                if (fields.Length != headers.Length)
                {
                    errors.Add(new PnlFileParseError(rowNumber, "The row has a different number of columns than the header."));
                    continue;
                }

                var sourceSystem = csv.GetField(headerMap["SourceSystem"])?.Trim();
                var accountValue = csv.GetField(headerMap["AccountNumber"]);
                var pnlValue = csv.GetField(headerMap["PnLAmount"]);
                if (string.IsNullOrWhiteSpace(sourceSystem))
                {
                    errors.Add(new PnlFileParseError(rowNumber, "SourceSystem is required."));
                    continue;
                }

                if (!int.TryParse(accountValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var accountNumber))
                {
                    errors.Add(new PnlFileParseError(rowNumber, "AccountNumber must be an integer."));
                    continue;
                }

                if (!decimal.TryParse(pnlValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var pnlAmount))
                {
                    errors.Add(new PnlFileParseError(rowNumber, "PnLAmount must be a decimal using invariant format."));
                    continue;
                }

                rows.Add(new PnlFileRow(sourceSystem, accountNumber, pnlAmount));
            }
        }
        catch (CsvHelperException exception)
        {
            errors.Add(new PnlFileParseError(0, $"The CSV structure could not be read: {exception.Message}"));
        }
        catch (DecoderFallbackException exception)
        {
            errors.Add(new PnlFileParseError(0, $"The CSV text encoding is invalid: {exception.Message}"));
        }

        return new PnlFileParseResult(rows, errors);
    }
}
