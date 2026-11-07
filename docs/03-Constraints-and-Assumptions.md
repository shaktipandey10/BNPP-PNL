# Constraints and Assumptions

This document records the operating assumptions for the initial P&L POC. Assumptions are not additional business requirements; items marked unresolved require confirmation before or during implementation.

## Environment and technology constraints

| Area | Constraint / assumption |
|---|---|
| Development memory | 12 GB RAM available; use a lightweight single-instance API, SQLite, one broker consumer, and sequential writes |
| Disk | Approximately 9 GB free; avoid unnecessary local infrastructure and large generated data sets |
| Development environment | Windows with Visual Studio Code |
| Backend | .NET 8, ASP.NET Core Web API, Clean Architecture modular monolith |
| Frontend | Angular and TypeScript |
| Persistence | EF Core with SQLite |
| Messaging | CloudAMQP RabbitMQ; no Docker or local RabbitMQ requirement |
| Consumer | One ASP.NET Core `BackgroundService`, prefetch count 1 |
| Producer | Separate .NET Console application reading sample JSON |
| Testing | xUnit and Moq |

## Runtime and infrastructure assumptions

- The initial POC runs as one API instance. No horizontal scaling is required.
- CloudAMQP availability, plan limits, TLS requirements, and queue policy support are external dependencies.
- RabbitMQ credentials and connection strings are stored in .NET User Secrets for local development or supplied through environment variables. They are never committed to source control.
- The producer and API need access to the same broker endpoint and compatible message contract.
- The initial consumer processes messages sequentially. Prefetch count 1 limits in-flight deliveries; it does not make SQLite a multi-writer database.
- The Angular UI reaches the API and SignalR hub over configured development origins.

## File layout assumptions

These are proposed defaults, not confirmed external file specifications:

- Supported formats are CSV and `.xlsx`.
- Column names correspond to `SourceSystem`, `AccountNumber`, and `PnLAmount`; header matching and whitespace/case behavior need confirmation.
- Excel uses the first worksheet and a header row.
- The complete file is parsed before any row is processed.
- Missing headers, malformed rows, and unparseable values are structural errors. If any occur, reject the file and process no rows.
- A zero P&L value is parseable business data, not a structural error; persist it as invalid.
- Files contain approximately 100–200 records.
- No file-level duplicate detection or upload batch tracking is included initially.

## Business and data assumptions

- `SourceSystem` is a string and is persisted separately from `FeedType`.
- `AccountNumber` is an integer. Leading-zero identifiers are not supported unless this assumption changes.
- `PnLAmount` is a decimal. Zero is invalid; any non-zero value, including negative values, is valid under the stated rule.
- Valid and invalid business records are both persisted.
- `ValidationReason` describes the business-invalid condition; file structural errors are returned as import errors and are not represented as persisted P&L records.
- `FeedType` is assigned by the ingestion path: `File` for uploaded files, `Messaging` for RabbitMQ.
- `ProcessedAtUtc` is recorded in UTC.
- Messaging records carry a stable `MessageId`, protected by a unique database constraint. File records do not have a message ID.
- File re-upload duplicate prevention is out of scope initially.
- File rows are processed sequentially with one transaction per record. A database failure during a batch can leave earlier rows committed.
- RabbitMQ records are ACKed only after successful persistence. A duplicate `MessageId` is treated as already processed and ACKed.
- SignalR notification is attempted after commit. If it is missed, Angular reloads from the report API after reconnection.

## SQLite and decimal assumptions

SQLite is selected for local, lightweight POC operation. It serializes writes; it is not suitable for unrestricted concurrent writers. Writes remain sequential and transactions short.

SQLite does not provide the same fixed-precision decimal type and arithmetic guarantees as SQL Server. The proposed POC representation is an invariant-culture decimal string, with the zero comparison performed in .NET. Do not rely on SQLite monetary aggregation or numeric ordering over this representation.

The required decimal scale/range has not been specified. If database-side calculations are later required, confirm a fixed scale and evaluate scaled integer storage or migrate to a database with fixed-precision decimal support.

## Unresolved business and implementation decisions

1. **File header contract:** Exact header spelling, case/whitespace handling, and whether CSV headers may be aliased.
2. **CSV culture and decimal format:** Whether decimal input uses invariant notation (for example `1234.56`) or a specified locale; expected decimal scale and maximum range.
3. **Account number semantics:** Confirm that account numbers are always numeric and that leading zeros do not carry meaning.
4. **Excel conventions:** Confirm first worksheet/header-row assumptions and how blank rows are handled.
5. **Structural-error response:** The current policy is whole-file rejection with no processing if any row is structurally invalid. Confirm this matches user expectations.
6. **Partial database failure:** Per-record transactions can leave earlier records committed if a later record fails. Confirm this is acceptable for the POC.
7. **Repeated producer samples:** Decide whether repeated publication of a sample row represents the same logical message (same `MessageId`) or a new event (new stable ID).
8. **Retry policy:** Confirm retry limit, delay, and CloudAMQP plan support for the required TTL/dead-letter routing.
9. **Upload limits and report paging:** Set request-size and page-size limits during implementation.
10. **Authentication and deployment exposure:** The POC assumes a trusted local/demo environment; deployment beyond that requires security decisions.

## Explicitly out of scope

Kafka implementation, Docker/local RabbitMQ, Redis, Kubernetes, microservices, Outbox Pattern in the initial POC, production-grade authentication, and horizontal scaling are not part of the approved initial scope.
