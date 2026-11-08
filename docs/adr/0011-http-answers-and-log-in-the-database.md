# ADR-0011: HTTP answers and the application's log go to the database for 14 days; the app cleans them itself

- **Status:** Accepted
- **Date:** 2026-11-08
- **Evidence:** the commit that adds spec 010
- **Specs:** 010

## Context
The team wants to see how the app is doing without a shell on the server: how many requests ended with 200, how many
with 403, how many with 500, which addresses fail, what the log says, what PostgreSQL is doing. Halcyon on the swarm has
no log stack of its own (the console log disappears with the container), and Halycon in the Ambit platform already has
a System menu (queues, database) people know. The record must not grow forever and must never make the app slower or
stop it.

## Decision
- Two plain tables in the schema `halcyon`, created by the migrator with the rest of the store
  (`MonitoringSchema`, Marten `ExtendedSchemaObjects`, ADR-0002): `http_requests` (time, method, path without the
  query string, status, elapsed ms, agent, trace id) and `app_logs` (time, level, source, message, exception, request
  path, trace id). Indexes on `at` and on `(status|level, at)`.
- **Every** HTTP answer is recorded - success or error - by a middleware first in the pipeline (`RequestRecording`),
  so it sees the status the client got (after the exception handler, after the 404 page). Probes, Blazor's circuit and
  static files are skipped (`Monitoring__IgnoredPaths`).
- The log goes to the table through a Serilog sink (`AppLogSink`) from `Monitoring__LogLevel` (Information) up; the
  one-line-per-request log and the monitoring's own entries stay out.
- Neither the request nor the logger touches the database: both put an entry into a bounded queue in memory
  (`MonitoringQueue`, `Monitoring__QueueCapacity`, drop and count when full); `MonitoringWriter` (a hosted service)
  writes the queues with binary COPY every `Monitoring__FlushInterval` (2 s) and once more at shutdown. A refused batch
  is dropped and counted, not retried.
- **Retention 14 days** (`Monitoring__Retention`). `MonitoringCleanup` (a hosted service) runs right after the start and
  every `Monitoring__CleanupInterval` (1 h), deleting older rows in batches of `Monitoring__CleanupBatchSize`. The cutoff
  comes from `TimeProvider`; the schedule from the real clock. Idempotent, so replicas need no lock.
- The System menu (every signed-in agent; the app has no roles, ADR-0005): **Status systemu** (health, 200 / 3xx / 403 /
  404 / other 4xx / 500 and 5xx, average and p95, a chart per hour or day, codes, the most frequent errors, the list of
  answers with filters, the effective settings with their variables), **Logi**, **Baza danych** (server, sessions from
  `pg_stat_activity` with blocking and long-running marked, tables, the event store), **Kolejki** (the two queues, the
  background jobs with "Uruchom teraz", Marten's event progression).
- Settings are technical, so environment only, validated at start; the status page shows them read-only.

## Alternatives considered
- **Documents in Marten.** Counting by status and deleting by time on jsonb is slower and a document per request is
  heavier than a row; nothing here needs a document's shape.
- **Writing from the request.** Simple, but every request would wait for an INSERT and a slow database would slow every
  page; the bounded queue makes the cost fixed.
- **A log stack (Loki, Seq) or OpenTelemetry to a collector.** The better answer once the platform has one; today it
  would be another service to run for one app. The console log stays, so adding it later changes nothing here.
- **RabbitMQ queues on the Kolejki page, as in Halycon.** Halcyon has no broker; the page shows what this app really
  queues.
- **Table partitioning by day and DROP PARTITION.** The right tool for big volumes; for one team's desk a DELETE by the
  index is enough and needs no partition management.

## Consequences
- Up to `QueueCapacity` entries (or one interval) can be lost if the process dies hard; dropped and failed entries are
  visible on the Kolejki page.
- The counts are per process start for the queues, but the tables are shared: with replicas the status page sums them.
- Agents can read the log; the log must therefore keep the rule of never holding passwords or form contents.
- Without `pg_read_all_stats` the database page cannot show other roles' queries (PostgreSQL hides them; the page says so).
- Tests: `preserveStaticLogger: true` - two hosts in one process used to share the static Serilog logger and wrote into
  each other's log.

## Revisit when
1. The tables grow past a few million rows (then partitions by day) or the status page gets slow.
2. The platform gets a log stack or an OpenTelemetry collector.
3. Agents get roles - then the System menu becomes an administrator's.
