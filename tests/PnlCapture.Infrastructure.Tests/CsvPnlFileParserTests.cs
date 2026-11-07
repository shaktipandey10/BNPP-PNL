using System.Text;
using PnlCapture.Infrastructure.Files;

namespace PnlCapture.Infrastructure.Tests;

public sealed class CsvPnlFileParserTests
{
    [Fact]
    public async Task ParseAsync_ParsesValidAndZeroAmountRows()
    {
        const string csv = "SourceSystem,AccountNumber,PnLAmount\nBloomberg,1001,125.75\nReuters,1002,0\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await new CsvPnlFileParser().ParseAsync(stream, CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(125.75m, result.Rows[0].PnlAmount);
        Assert.Equal(0m, result.Rows[1].PnlAmount);
    }

    [Fact]
    public async Task ParseAsync_MissingRequiredHeaderReturnsStructuralError()
    {
        const string csv = "SourceSystem,AccountNumber\nBloomberg,1001\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await new CsvPnlFileParser().ParseAsync(stream, CancellationToken.None);

        Assert.Empty(result.Rows);
        Assert.Contains(result.Errors, error => error.Message.Contains("PnLAmount", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_UnparseableValueReturnsStructuralError()
    {
        const string csv = "SourceSystem,AccountNumber,PnLAmount\nBloomberg,not-an-integer,1.25\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await new CsvPnlFileParser().ParseAsync(stream, CancellationToken.None);

        Assert.Empty(result.Rows);
        Assert.Contains(result.Errors, error => error.Message.Contains("AccountNumber", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_DuplicateHeaderReturnsStructuralError()
    {
        const string csv = "SourceSystem,AccountNumber,PnLAmount,pnlAmount\nBloomberg,1001,1.25,2.50\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await new CsvPnlFileParser().ParseAsync(stream, CancellationToken.None);

        Assert.Empty(result.Rows);
        Assert.Contains(result.Errors, error => error.Message.Contains("Duplicate header", StringComparison.Ordinal));
    }
}
