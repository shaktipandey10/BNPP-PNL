using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PnlCapture.Api.Hubs;
using PnlCapture.Api.Notifications;
using PnlCapture.Application;
using PnlCapture.Application.Abstractions.Notifications;
using PnlCapture.Infrastructure;
using PnlCapture.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var sqliteConnection = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("PnlDatabase")
    ?? "Data Source=data/pnlcapture.db;Default Timeout=5;Cache=Shared");
if (sqliteConnection.DataSource != ":memory:" &&
    !sqliteConnection.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
{
    if (!Path.IsPathRooted(sqliteConnection.DataSource))
    {
        sqliteConnection.DataSource = Path.GetFullPath(
            sqliteConnection.DataSource,
            builder.Environment.ContentRootPath);
    }

    var databaseDirectory = Path.GetDirectoryName(sqliteConnection.DataSource);
    if (!string.IsNullOrEmpty(databaseDirectory))
    {
        Directory.CreateDirectory(databaseDirectory);
    }
}

builder.Configuration["ConnectionStrings:PnlDatabase"] = sqliteConnection.ToString();

builder.Services.AddProblemDetails();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options =>
    options.AddPolicy("AngularDevelopment", policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
builder.Services.AddPnlApplication();
builder.Services.AddPnlInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IPnlRealtimeNotifier, SignalRPnlRealtimeNotifier>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("AngularDevelopment");
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHub<PnlHub>("/hubs/pnl");

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<PnlDbContext>();
    await database.Database.EnsureCreatedAsync();
    await database.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL");
}

app.Run();

public partial class Program
{
}
