# P&L Capture, Processing and Reporting Requirements

## 1. Purpose

This document defines the approved requirements for the Real-Time P&L Capture, Processing and Reporting POC. It complements the architecture and design documents; it does not introduce additional implementation scope.

## 2. Scope

The POC captures P&L data using two independent ingestion mechanisms:

1. Start-of-day CSV and Excel file uploads through an Angular UI.
2. Periodic RabbitMQ messages published by a separate .NET Console Producer and consumed by the ASP.NET Core backend.

The backend applies a common business rule, persists valid and invalid records in SQLite, exposes reports, and broadcasts committed record updates to the Angular UI through SignalR.

### In scope

- Angular frontend.
- ASP.NET Core Web API on .NET 8.
- Clean Architecture organized as a modular monolith.
- EF Core and SQLite.
- CSV and Excel ingestion.
- CloudAMQP RabbitMQ.
- Separate .NET Console Producer reading sample records from JSON.
- One ASP.NET Core `BackgroundService` consumer with prefetch count 1.
- Shared application processing service for both ingestion paths.
- SignalR updates after successful persistence.
- xUnit and Moq testing.

### Out of scope for the initial POC

- Kafka implementation, Docker/local RabbitMQ, Redis, Kubernetes, and microservices.
- Outbox Pattern.
- File-level duplicate detection and import batch tracking.
- Horizontal scaling and multiple consumers.
- Production authentication, authorization, monitoring, and operational guarantees.

## 3. Actors and external systems

| Actor/system | Role |
|---|---|
| User | Uploads a file and views P&L reports in the Angular UI |
| Angular application | Provides upload, reporting, filtering, and live-update UI |
| Console Producer | Reads sample JSON records and publishes messages |
| CloudAMQP RabbitMQ | Delivers messages from the Producer to the backend consumer |
| ASP.NET Core API | Ingests files, serves reports, and hosts SignalR |
| Background consumer | Processes RabbitMQ messages in the backend |
| SQLite database | System of record for processed P&L data |

## 4. Data requirements

Each P&L record has these business input fields:

| Field | Type | Requirement |
|---|---|---|
| `SourceSystem` | String | Identifies the originating system |
| `AccountNumber` | Integer | Identifies the account |
| `PnLAmount` | Decimal | Financial value used by the validation rule |

Each persisted record also maintains:

| Field | Requirement |
|---|---|
| `IsValid` | Stores the outcome of the P&L business rule |
| `ValidationReason` | Describes the business-invalid outcome; nullable for valid records |
| `FeedType` | Identifies `File` or `Messaging` ingestion |
| `ProcessedAtUtc` | Stores processing time in UTC |
| `MessageId` | Stores the stable message identity for messaging records; nullable for file records |

The initial SQLite schema contains only the `PnlRecords` table. Messaging `MessageId` values must have a unique database constraint when present.

## 5. Business rules

| Rule ID | Requirement |
|---|---|
| BR-01 | If `PnLAmount` equals zero, classify the record as invalid. |
| BR-02 | If `PnLAmount` is non-zero, classify the record as valid. |
| BR-03 | Persist both valid and business-invalid records. |
| BR-04 | A zero P&L value is business-invalid data, not a file parsing/structural error. |
| BR-05 | Apply the same business rule through the same application processing service for file and messaging ingestion. |
| BR-06 | Preserve the ingestion source separately from the source system: `FeedType` is `File` or `Messaging`; `SourceSystem` remains the supplied business field. |
| BR-07 | Messaging records require a stable `MessageId`; repeated delivery of the same ID must not create another record. |

## 6. Functional requirements

### File ingestion

| ID | Requirement |
|---|---|
| FR-FILE-01 | The Angular UI shall allow a user to upload a CSV or Excel file. |
| FR-FILE-02 | The system shall support files containing approximately 100–200 records. |
| FR-FILE-03 | The backend shall parse and normalize records into the common P&L input shape. |
| FR-FILE-04 | The backend shall parse the complete file before beginning record processing. |
| FR-FILE-05 | Missing headers, malformed rows, and unparseable values shall be reported as structural errors, distinct from business validation outcomes. |
| FR-FILE-06 | If the file contains structural/parsing errors, the file shall be rejected before any row is processed. |
| FR-FILE-07 | After successful parsing, records shall be processed sequentially through the shared application processing service with `FeedType = File`. |
| FR-FILE-08 | A database failure during sequential file processing may leave earlier records committed; whole-file transactional rollback is not required in the initial POC. |
| FR-FILE-09 | Preventing duplicate file uploads is not required in the initial POC. |

### Messaging ingestion

| ID | Requirement |
|---|---|
| FR-MSG-01 | A separate .NET Console Producer shall read sample P&L records from a local JSON file. |
| FR-MSG-02 | The Producer shall publish individual messages periodically to CloudAMQP RabbitMQ. |
| FR-MSG-03 | Each messaging record shall include a stable `MessageId`. |
| FR-MSG-04 | The ASP.NET Core application shall consume messages using a hosted `BackgroundService`. |
| FR-MSG-05 | The initial deployment shall use one consumer with prefetch count 1 and sequential processing. |
| FR-MSG-06 | The consumer shall call the same application processing service as file ingestion, with `FeedType = Messaging`. |
| FR-MSG-07 | The consumer shall ACK a message only after successful persistence or after recognizing that its `MessageId` was already processed. |
| FR-MSG-08 | The messaging flow shall support bounded retry and a Dead Letter Queue (DLQ); messages shall not be retried indefinitely. |

### Persistence, reporting, and live updates

| ID | Requirement |
|---|---|
| FR-DATA-01 | The system shall persist all records that pass structural parsing, including business-invalid records. |
| FR-DATA-02 | The system shall persist `SourceSystem`, `AccountNumber`, `PnLAmount`, validation outcome/reason, feed type, and UTC processing time. |
| FR-DATA-03 | The system shall enforce uniqueness for non-null messaging `MessageId` values. |
| FR-REPORT-01 | The UI shall provide a valid P&L records grid. |
| FR-REPORT-02 | The UI shall provide an invalid P&L records grid. |
| FR-REPORT-03 | Reports shall support filtering by source system and feed type. |
| FR-REPORT-04 | The UI shall show total, valid, and invalid summary counts. |
| FR-REALTIME-01 | The backend shall send SignalR notifications only after the record is successfully committed to the database. |
| FR-REALTIME-02 | The Angular UI shall update its displayed data automatically when it receives a SignalR event. |
| FR-REALTIME-03 | After SignalR reconnection, Angular shall reload report data from the API because SignalR is not the system of record. |

## 7. Non-functional requirements

| ID | Requirement |
|---|---|
| NFR-ARCH-01 | The backend shall follow Clean Architecture dependency direction with Domain, Application, Infrastructure, and Presentation layers. |
| NFR-ARCH-02 | The backend shall be a modular monolith, not a set of microservices. |
| NFR-ARCH-03 | Both ingestion paths shall reuse the same application processing service. |
| NFR-PERF-01 | Processing and database operations shall use asynchronous I/O where supported. |
| NFR-PERF-02 | Initial database writes and message processing shall be sequential to limit SQLite write contention. |
| NFR-REL-01 | RabbitMQ processing shall use bounded retry and DLQ handling. |
| NFR-REL-02 | Messaging idempotency shall be enforced using stable `MessageId` values and a database uniqueness constraint. |
| NFR-REL-03 | Persistence shall complete before SignalR notification is attempted. |
| NFR-DATA-01 | P&L values shall use `decimal` in application/business logic. |
| NFR-DATA-02 | SQLite decimal limitations shall be addressed with a SQLite-compatible representation; database-side financial aggregation is not assumed. |
| NFR-SEC-01 | CloudAMQP credentials shall be stored using .NET User Secrets or environment variables, not committed to source control. |
| NFR-TEST-01 | Automated tests shall use xUnit and Moq for applicable unit tests, with integration tests for persistence and API behavior. |
| NFR-OPS-01 | The initial development setup shall not require Docker, local RabbitMQ, Redis, Kubernetes, or other unapproved infrastructure. |

## 8. Acceptance criteria

The POC meets its core requirements when:

1. A structurally valid CSV or Excel file can be uploaded and its records are processed sequentially.
2. Missing headers, malformed rows, or unparseable values are reported as structural errors and prevent the file from being processed.
3. A parsed zero P&L value is persisted as invalid with a validation reason; a non-zero value is persisted as valid.
4. File and RabbitMQ records use the same application processing service and validation behavior.
5. The Console Producer can publish sample JSON records to CloudAMQP, and the backend consumer processes them with prefetch 1.
6. Redelivery of a messaging record with the same `MessageId` does not create a duplicate record.
7. The consumer ACKs only after persistence succeeds or a duplicate is safely recognized; bounded retry and DLQ handling are configured.
8. The API returns valid and invalid reports, filters by source system and feed type, and provides total/valid/invalid counts.
9. The UI updates after a SignalR event for a committed record and reloads report data after reconnecting.
10. The five-day POC environment uses the approved stack and does not depend on Docker or local RabbitMQ.

## 9. Constraints and assumptions

- Development environment: Windows, VS Code, .NET 8, Angular, approximately 12 GB RAM and 9 GB free disk space.
- File volume is approximately 100–200 rows per file.
- The initial backend is one instance with one consumer; no horizontal scaling is required.
- SQLite is the initial database and has single-writer limitations.
- The initial file policy is all-or-nothing for structural parsing but sequential per-record persistence after parsing.
- File re-upload duplicate prevention is excluded.
- SignalR is a best-effort live notification channel; reports in SQLite are authoritative.
- The Outbox Pattern is deferred.

## 10. Unresolved requirements and decisions

The following details are not defined by the approved requirements and must not be assumed as business rules:

1. Exact CSV headers, permitted header variations, delimiter, encoding, and decimal culture.
2. Excel worksheet selection, header row, blank-row handling, and supported Excel variants.
3. Required decimal scale/range and whether P&L values need database-side sorting or aggregation.
4. Whether `AccountNumber` may contain leading zeros or should instead be treated as text.
5. Whether rejecting an entire structurally invalid file is acceptable in all cases.
6. Whether partial commits following a database failure during batch processing are acceptable beyond the POC.
7. Retry count and delay, and confirmation that the selected CloudAMQP plan supports the desired queue configuration.
8. Whether repeated producer sample rows represent the same logical message or new events with new IDs.
9. Authentication, authorization, API exposure, report paging, and upload-size limits for any non-local deployment.
