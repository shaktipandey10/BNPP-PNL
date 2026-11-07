# POC Limitations

These limitations are accepted for the initial proof of concept. They should be revisited before production use or significant growth.

## Persistence and processing

- **SQLite single writer:** SQLite serializes writes. Concurrent uploads, consumer processing, and report activity can contend for the write lock. The initial design uses sequential writes, short transactions, and one consumer; it is not intended for high write throughput.
- **SQLite decimal behavior:** The POC uses `decimal` in .NET and a SQLite-compatible representation. SQLite does not provide SQL Server-style fixed-precision decimal semantics; database-side P&L aggregation and ordering are not reliable goals for this version.
- **Per-record file commits:** The complete file is parsed before processing, but rows are committed one at a time. A database failure partway through processing may leave earlier records committed. The initial design does not provide whole-file transactional rollback.
- **No file duplicate detection:** Uploading the same file again may create duplicate file records. Message idempotency applies only to messaging records.
- **Single PnL rule:** Only zero/non-zero validity is included. Additional business validation is not implied.

## Deployment and scale

- **Single-instance modular monolith:** API, hosted consumer, and SignalR hub run in one backend process. This keeps the POC simple but couples their deployment and lifecycle.
- **Single RabbitMQ consumer:** One consumer with prefetch count 1 processes sequentially. Throughput is intentionally limited to reduce SQLite write contention.
- **No horizontal scaling:** Multiple API instances or consumers are not part of the initial deployment. SQLite file access and SignalR client distribution would require a redesigned persistence and notification approach.
- **CloudAMQP dependency:** Broker availability, quotas, connection limits, plan-specific queue features, and network access depend on the selected CloudAMQP plan. These must be checked before configuring retry/DLQ topology.

## Notifications and user experience

- **Direct SignalR delivery is not durable:** A process crash after a database commit and before the SignalR send can cause a connected client to miss the event. The record remains persisted; Angular must reload report data after reconnect. The Outbox Pattern is deferred.
- **No replay stream:** SignalR is for live updates, not event history or system-of-record queries.
- **File structural errors reject the upload:** Missing headers, malformed rows, or unparseable values prevent processing of the file. Business-invalid zero P&L rows are persisted, but the POC does not accept a structurally partial file.
- **No import batch tracking:** The initial schema has only `PnlRecords`; it does not track file-level status, batch summaries over time, or original file metadata.

## Security and operations

- **No production authentication initially:** Do not expose the POC API publicly without adding authentication and authorization.
- **Credential handling is operationally dependent:** RabbitMQ secrets must be set using User Secrets or environment variables. Accidental logging or misconfiguration remains a deployment risk.
- **Limited monitoring:** Structured application logging and basic error reporting are expected, but no production-grade metrics, tracing, alerting, or dashboard is included.
- **Bounded but basic retry:** Retry and DLQ handling covers transient and poison-message scenarios at POC scale. It is not a full operational replay or incident-management system.
- **Local file/database lifecycle:** Backups, retention, recovery objectives, and durable hosting of the SQLite file are not defined for production.
- **Input size and abuse controls:** Upload-size limits and basic validation are required, but a full production threat model, rate limiting, and malware scanning are not included.

## Production readiness

This POC demonstrates ingestion, shared validation, persistence, reporting, and live notification. It does not by itself establish production availability, high throughput, multi-instance resilience, financial audit compliance, disaster recovery, or operational support.
