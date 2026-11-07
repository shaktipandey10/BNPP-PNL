# P&L Capture POC

This repository contains an ASP.NET Core API, an Angular dashboard, SQLite persistence, and a separate .NET Console Producer for RabbitMQ ingestion.

## Quick run commands

Run these from separate PowerShell terminals at the repository root. Start the API first:

**Terminal 1 — API:**

```powershell
dotnet run --project .\src\PnlCapture.Api\PnlCapture.Api.csproj --launch-profile http
```

**Terminal 2 — Producer:**

```powershell
dotnet run --project .\src\PnlCapture.Producer\PnlCapture.Producer.csproj
```

For RabbitMQ ingestion, configure `RabbitMq__ConnectionUri` in the API terminal and `RABBITMQ_URI` in the Producer terminal with the CloudAMQP AMQP URL. See [Run with CloudAMQP](#run-with-cloudamqp) for secure setup instructions.

## Run with CloudAMQP

### 1. Get the broker URL

In the CloudAMQP dashboard, open your instance and copy its **AMQP URL**. Use the complete URL for both the API and Producer; neither expects separate host, port, username, password, or virtual-host settings. Prefer the `amqps://` URL if CloudAMQP provides one. The RabbitMQ client supports `amqp://` and `amqps://`; the latter uses TLS.

The URL contains credentials and the virtual host. Keep it private, preserve URL-encoded characters, and do not paste it into source files, screenshots, or shared logs.

### 2. Start the API and its RabbitMQ consumer

The API reads `RabbitMq:ConnectionUri` from configuration. Set the corresponding environment variable, `RabbitMq__ConnectionUri`, before starting the API. If the API is already running, stop it with **Ctrl+C** first; it must restart to inherit the variable.

In **Terminal 1**, enter:

```powershell
$secure = Read-Host "CloudAMQP AMQP URL (input hidden)" -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $env:RabbitMq__ConnectionUri = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    Set-Location 'D:\repos\repos\BNPP'
    dotnet run --project .\src\PnlCapture.Api\PnlCapture.Api.csproj --launch-profile http
}
finally {
    Remove-Item Env:\RabbitMq__ConnectionUri -ErrorAction SilentlyContinue
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    $secure.Dispose()
}
```

Enter the AMQP URL at the hidden prompt. The API starts its RabbitMQ background consumer automatically. Look for a log indicating that it started for `pnl.records`. If no `ConnectionUri` is configured, the consumer logs that it is disabled. Connection failures are logged and retried after five seconds; RabbitMQ automatic recovery is enabled as well. Avoid sharing raw connection-error logs without checking for sensitive data.

### 3. Understand the automatically declared topology

When the consumer connects, it declares the exchange, queues, and bindings in the virtual host specified by the AMQP URL. You do not need to create them manually in CloudAMQP.

| RabbitMQ item | Value |
|---|---|
| Exchange | `pnl.exchange` (durable direct exchange) |
| Main queue | `pnl.records` (durable) |
| Main routing key | `pnl.record` |
| Retry queue | `pnl.retry` (5-second message TTL) |
| Dead-letter queue | `pnl.dead` |
| Maximum retries | 3 |

Start the API consumer before the Producer. The Producer declares the exchange but does not declare the main queue or its binding.

### 4. Run the separate Console Producer

The Producer reads the `RABBITMQ_URI` environment variable. It uses the bundled `SampleData\pnl-records.json` file, which is copied into its build output by the project. It publishes all four records one at a time, waiting one second between records by default. Each sample has a fixed `messageId`; the Producer preserves it rather than generating a new ID.

In **Terminal 2**, enter:

```powershell
$secure = Read-Host "CloudAMQP AMQP URL (input hidden)" -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $env:RABBITMQ_URI = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    Set-Location 'D:\repos\repos\BNPP'
    dotnet run --project .\src\PnlCapture.Producer\PnlCapture.Producer.csproj
}
finally {
    Remove-Item Env:\RABBITMQ_URI -ErrorAction SilentlyContinue
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    $secure.Dispose()
}
```

The Producer should print one `Published ...` line per sample, followed by `Published 4 records.` This means RabbitMQ confirmed the publishes; it does not by itself prove the API consumed or persisted the messages.

### 5. Verify the end-to-end flow

1. **Consumer connection:** In Terminal 1, check for the consumer-started log naming `pnl.records`.
2. **Publishing:** In Terminal 2, check for four published records.
3. **Consumption and persistence:** The API processes consumed records into SQLite. The default database is `src\PnlCapture.Api\data\pnlcapture.db` when running the API from this repository root.
4. **Dashboard updates:** Open or refresh the Angular dashboard at `http://localhost:4200`. It connects to the API at `http://localhost:5020`; the dashboard should show the new records and its live connection status.
5. **Valid and invalid records:** The sample contains three non-zero P&Ls and one zero P&L. Non-zero records are Valid; `sample-message-003` has `pnlAmount: 0` and should appear as Invalid. Both are persisted.
6. **Summary counts:** Query the API:

   ```powershell
   Invoke-RestMethod 'http://localhost:5020/api/reports/summary' | Format-List
   ```

   For a fresh database and no other incoming records, the sample adds 4 total, 3 valid, and 1 invalid record. With existing records, compare the summary before and after publishing.

### Reset the SQLite database before testing

The API's default database is `src\PnlCapture.Api\data\pnlcapture.db`. Stop the API with **Ctrl+C** before moving the database so SQLite has closed it. From the repository root, run this PowerShell script to keep a timestamped backup, including any SQLite WAL sidecar files:

```powershell
$db = '.\src\PnlCapture.Api\data\pnlcapture.db'
$backup = "$db.backup-$(Get-Date -Format yyyyMMdd-HHmmss)"

if (Test-Path $db) {
    Move-Item $db $backup
}

foreach ($suffix in '-wal', '-shm') {
    $sidecar = "$db$suffix"
    if (Test-Path $sidecar) {
        Move-Item $sidecar "$backup$suffix"
    }
}
```

Restart the API with the CloudAMQP URL configured. The API creates a fresh database on startup. Confirm the summary is empty before publishing:

```powershell
Invoke-RestMethod 'http://localhost:5020/api/reports/summary' | Format-List
```

The summary should show zero total, valid, and invalid records. The root-level `src\PnlCapture.Api\pnlcapture.db` is not the configured database unless the connection string has been overridden (for example, with `ConnectionStrings__PnlDatabase`).

### 6. Demonstrate duplicate MessageId handling

The Producer publishes the entire sample file each time it runs. The sample IDs are stable, so running it twice republishes the exact same MessageIds without changing the business logic. The zero-P&L sample is `sample-message-003`; it is a useful check that an Invalid record is deduplicated too.

Before running the Producer, capture the persisted summary:

```powershell
$before = Invoke-RestMethod 'http://localhost:5020/api/reports/summary'
$before | Format-List
```

Run the Producer once, then capture the summary:

```powershell
$afterFirstRun = Invoke-RestMethod 'http://localhost:5020/api/reports/summary'
$afterFirstRun | Format-List
```

Run the Producer a second time with the same sample file and URL, then check again:

```powershell
$afterSecondRun = Invoke-RestMethod 'http://localhost:5020/api/reports/summary'
$afterSecondRun | Format-List
```

The first run adds records only for MessageIds that were not already persisted. The second run should not increase counts for those IDs. If the IDs were already in the database, neither run should increase the count. The consumer logs when it acknowledges a duplicate. The persisted summary provides a database-backed count check.

### 7. CloudAMQP values and permissions

From the CloudAMQP instance details, use the complete **AMQP URL** for both processes. If reviewing its components, the URL contains:

- **Hostname** and **port** — broker endpoint.
- **Username** and **password** — broker credentials.
- **Virtual host** — URL path; both processes must use the same one.
- **Scheme** — `amqp://` or preferably `amqps://` when offered.

Do not set these components separately in this application. Ensure the CloudAMQP user has the permissions needed to connect, declare the exchange/queues/bindings, consume, and publish in that virtual host. The consumer declares the POC topology; manual topology creation is not required.

### 8. If CloudAMQP is unavailable

These features can still be demonstrated without RabbitMQ credentials:

- CSV/Excel upload through Angular.
- SQLite persistence for uploaded records.
- Valid and Invalid reports and summary counts.
- Angular dashboard and SignalR updates for file imports.
- Automated MessageId idempotency tests.

For automated tests, run from the repository root:

```powershell
dotnet test .\PnlCapture.sln --configuration Release
```

Live RabbitMQ publishing, consumption, retry, and dead-letter behavior require a reachable CloudAMQP broker and must be verified against it.

## Logging

The API uses ASP.NET Core's built-in `ILogger` logging providers. The configured minimum levels are in `src\PnlCapture.Api\appsettings.json`: `Information` for application logs and `Warning` for `Microsoft.AspNetCore` logs. By default, logs are written to the API terminal; this POC does not configure log-file output or centralized logging. API logs include RabbitMQ consumer startup, connection failures and retry attempts, malformed messages routed to the dead-letter queue, processing failures, and duplicate-message acknowledgements.

The Console Producer writes progress to its terminal using `Console.WriteLine`, including each published MessageId and the final record count. It does not use the API's `ILogger` configuration.

Terminal output is not retained by this application after the terminal closes. Avoid sharing logs without checking them for sensitive information; never share the CloudAMQP AMQP URL or credentials.

## Implementation references

- API RabbitMQ settings: [RabbitMqOptions.cs](src/PnlCapture.Infrastructure/Messaging/RabbitMqOptions.cs)
- Consumer and topology: [RabbitMqConsumerService.cs](src/PnlCapture.Infrastructure/Messaging/RabbitMqConsumerService.cs)
- Infrastructure registration: [DependencyInjection.cs](src/PnlCapture.Infrastructure/DependencyInjection.cs)
- API configuration: [appsettings.json](src/PnlCapture.Api/appsettings.json)
- Console Producer: [Program.cs](src/PnlCapture.Producer/Program.cs)
- Producer samples: [pnl-records.json](src/PnlCapture.Producer/SampleData/pnl-records.json)
