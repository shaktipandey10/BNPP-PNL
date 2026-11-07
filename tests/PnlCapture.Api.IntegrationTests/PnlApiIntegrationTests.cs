using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PnlCapture.Api.Contracts;
using PnlCapture.Infrastructure.Persistence;

namespace PnlCapture.Api.IntegrationTests;

public sealed class PnlApiIntegrationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"pnl-capture-tests-{Guid.NewGuid():N}.db");
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<PnlDbContext>>();
                    services.AddDbContext<PnlDbContext>(options =>
                        options.UseSqlite($"Data Source={_databasePath}"));
                }));
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Upload_ValidFilePersistsBothValidAndInvalidRecordsForReporting()
    {
        const string csv =
            "SourceSystem,AccountNumber,PnLAmount\nBloomberg,1001,125.50\nReuters,2002,0\n";
        using var content = CreateUpload(csv);

        var response = await _client.PostAsync("/api/imports", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        Assert.Equal(2, import.TotalRecords);
        Assert.Equal(1, import.ValidRecords);
        Assert.Equal(1, import.InvalidRecords);

        var summary = await _client.GetFromJsonAsync<PnlReportSummaryResponse>("/api/reports/summary");
        Assert.NotNull(summary);
        Assert.Equal(2, summary.TotalCount);
        Assert.Equal(1, summary.ValidCount);
        Assert.Equal(1, summary.InvalidCount);
    }

    [Fact]
    public async Task Upload_StructuralErrorRejectsWholeFileWithoutPersistingValidRows()
    {
        const string csv =
            "SourceSystem,AccountNumber,PnLAmount\nBloomberg,1001,125.50\nReuters,not-an-integer,0\n";
        using var content = CreateUpload(csv);

        var response = await _client.PostAsync("/api/imports", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        Assert.False(import.Succeeded);
        Assert.Contains(import.Errors, error =>
            error.RowNumber == 3 && error.Message.Contains("AccountNumber", StringComparison.Ordinal));

        var summary = await _client.GetFromJsonAsync<PnlReportSummaryResponse>("/api/reports/summary");
        Assert.NotNull(summary);
        Assert.Equal(0, summary.TotalCount);
    }

    [Fact]
    public async Task Upload_RejectsFilesLargerThanTenMegabytesWithPayloadTooLarge()
    {
        var content = new MultipartFormDataContent();
        content.Add(
            new ByteArrayContent(new byte[10 * 1024 * 1024 + 1]),
            "file",
            "oversized.csv");

        using (content)
        {
            var response = await _client.PostAsync("/api/imports", content);

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            var import = await response.Content.ReadFromJsonAsync<ImportResponse>();
            Assert.NotNull(import);
            Assert.Contains(import.Errors, error => error.Message.Contains("10 MB", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Reports_PagingReturnsAllValidRowsAndSummaryCountsTheWholeDataset()
    {
        const string csv =
            "SourceSystem,AccountNumber,PnLAmount\nBloomberg,1001,125.50\nReuters,2002,-42.25\nReuters,2003,0\n";
        using var content = CreateUpload(csv);
        var uploadResponse = await _client.PostAsync("/api/imports", content);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var firstPage = await _client.GetFromJsonAsync<List<PnlRecordResponse>>(
            "/api/reports/valid?page=1&pageSize=1",
            JsonOptions);
        var secondPage = await _client.GetFromJsonAsync<List<PnlRecordResponse>>(
            "/api/reports/valid?page=2&pageSize=1",
            JsonOptions);
        var summary = await _client.GetFromJsonAsync<PnlReportSummaryResponse>("/api/reports/summary");

        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.Single(firstPage);
        Assert.Single(secondPage);
        Assert.NotEqual(firstPage[0].Id, secondPage[0].Id);
        Assert.NotNull(summary);
        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(2, summary.ValidCount);
        Assert.Equal(1, summary.InvalidCount);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private static MultipartFormDataContent CreateUpload(string csv)
    {
        var content = new MultipartFormDataContent();
        content.Add(
            new StringContent(csv, Encoding.UTF8, "text/csv"),
            "file",
            "pnl.csv");
        return content;
    }
}
