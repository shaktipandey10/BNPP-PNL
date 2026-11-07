# Future Scope

The items below are potential enhancements after the initial POC. They are not part of the approved implementation unless separately prioritized.

## Data, processing, and messaging

- **Outbox Pattern:** Persist notification events transactionally with P&L records and dispatch asynchronously. This closes the direct SignalR post-commit delivery gap.
- **SQL Server migration:** Move to a database with fixed-precision decimal support, stronger concurrent-write characteristics, and production backup/operational tooling.
- **Controlled parallel consumers:** Increase RabbitMQ throughput only after measuring load and selecting persistence that supports the target concurrency. Preserve idempotency and ordering requirements.
- **Kafka alternative:** Consider Kafka for high-volume event streaming, replayable logs, partitioned ordering, or multiple independent consumer groups. Kafka is not implemented in the POC and would add operational and design complexity.
- **File import tracking:** Add import batch metadata, source file identity, row-level outcomes, and durable import summaries.
- **File duplicate detection:** Define a stable file/batch identity or content fingerprint and a retention policy before preventing repeated uploads.
- **Message contract evolution:** Add explicit schema versioning and compatibility rules if independent producers and consumers evolve.
- **Advanced audit history:** Record business rule versions, corrections, user actions, and immutable change history where required.

## User experience and reporting

- Add advanced filtering, paging, export, and configurable report views.
- Support richer validation reasons and configurable business rules if requirements expand.
- Improve upload feedback with per-row outcomes if partial file acceptance is approved.
- Consider durable client update recovery or report change cursors if snapshot reload becomes insufficient.

## Security and operations

- Add authentication and role-based authorization before broader or production exposure.
- Add advanced monitoring and observability: metrics, distributed tracing, structured correlation, dashboards, and actionable alerts.
- Define secrets rotation, broker credential management, data retention, backup/restore, and incident procedures.
- Add production-grade API rate limiting, upload controls, and security testing appropriate to the deployment.

## Deployment and scale

- Evaluate cloud deployment for the API, Angular application, and database.
- Plan horizontal scalability only after replacing SQLite and addressing shared SignalR connection management and broker-consumer ownership.
- Revisit service boundaries only if independent deployment or scaling needs justify a move beyond the modular monolith.

## Scope guardrail

Do not introduce Kafka, SQL Server, parallel consumers, an Outbox, cloud hosting, or other listed enhancements into the initial POC without an explicit scope decision. The current baseline remains Angular, ASP.NET Core Web API, one modular-monolith backend, SQLite, CloudAMQP RabbitMQ, one sequential consumer, direct post-commit SignalR notification, and a separate Console Producer.
