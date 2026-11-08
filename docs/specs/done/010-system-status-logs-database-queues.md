# 010 · System: status, logs, database, queues

**Status:** done · **Built:** 2026-11-08 → 2026-11-08

## Why
On the swarm nobody wants to open a shell to learn whether the app is fine. Like the System menu of Halycon (the Ambit
platform: queues, database), the team wants to see in the app how many requests succeed and fail (200, 403, 500), what
the log says and what the database is doing - and the record must not grow forever.

## What it promises
- A **System** section in the menu, for signed-in agents: **Status systemu** (`/system/status`), **Logi**
  (`/system/logi`), **Baza danych** (`/system/baza`), **Kolejki** (`/system/kolejki`).
- Every HTTP answer, success or error, goes to the database (`halcyon.http_requests`), without the query string;
  probes, Blazor's circuit and static files do not.
- The application's log from Information up goes to the database (`halcyon.app_logs`) with the exception text.
- Both are kept **14 days**; a background job removes older rows right after the start and every hour.
- **Status systemu**: version and uptime, health of the database and S3; a period of 24 hours / 7 days / 14 days; tiles
  for all answers (average, p95), 200, redirects, 403, 404 (+ other 4xx), 500 (+ all 5xx) and errors in the log; a chart
  of successes / 4xx / 5xx per hour or day with a table; counts per code; the ten most frequent errors; the list of
  answers filtered by outcome (all, success, 4xx, 403, 404, 5xx) and address, paged; the effective settings with their
  environment variables.
- **Logi**: newest first, level (Info and up, warnings and errors, errors only), search in message, source and exception;
  the exception folds out.
- **Baza danych**: version, uptime, size, connections, sessions of this database with whom they wait for, blocking and
  long-running marked, the query cut to 500 characters; tables of `halcyon` and `events` with rows and size; the event
  store's last number, streams and last event.
- **Kolejki**: the two in-memory queues (waiting / capacity, accepted, written, dropped, failed, last write), the two
  background jobs (runs, failures, last result, next run, **Uruchom teraz**), Marten's event progression.

## Surface
- Database: `Halcyon.Database/Monitoring/MonitoringSchema.cs` (two tables in the store's schema, applied by the migrator).
- App: `Monitoring/*` - `MonitoringOptions`, `MonitoringQueue(s)`, `RequestRecording`, `AppLogSink`, `MonitoringWriter`,
  `MonitoringCleanup`, `BackgroundJobs`, `MonitoringQueries`, `DatabaseActivity`, `SystemText`; pages
  `Components/Pages/SystemPages/*`, `Ui/Tile.razor`, `Ui/Pager.razor`, the menu.
- Configuration: `Monitoring__*` (README).

## Out of scope
- Roles: every agent sees the System pages (the app has none, ADR-0005).
- Purging a queue, peeking messages, dead letters (no broker in Halcyon).
- A history of database samples over time (Halycon's sampler) - only "now".
- Exporting the log.

## Acceptance criteria
- [x] Every answer, success or error, is recorded; probes are not — `tests/Halcyon.IntegrationTests/MonitoringTests.cs::EveryAnswer_SuccessOrError_GoesToTheDatabase_ButNotTheProbes`
- [x] The query string is not recorded — `MonitoringTests.TheQueryString_IsNotRecorded`
- [x] 200 / 3xx / 403 / 404 / other 4xx / 500 / 5xx counted in the period, per hour and per day — `MonitoringTests.Statistics_Count200_403_404_And500_InTheRange`
- [x] The log from Information up with the exception, request lines left out — `MonitoringTests.TheLog_FromInformationUp_GoesToTheDatabase_WithTheException`
- [x] The cleanup removes only rows older than 14 days, in batches, and reports what it did — `MonitoringTests.Cleanup_RemovesOnlyWhatIsOlderThan14Days`, `Cleanup_WorksInBatches`
- [x] A full queue drops and counts — `MonitoringTests.AFullQueue_DropsNewEntries_AndCountsThem`
- [x] Production defaults; a wrong setting stops the start with the field's name — `MonitoringTests.TheDefaults_AreTheProductionOnes`, `AWrongSetting_StopsTheStart_WithTheReason`
- [x] The database page reads the server, the tables and the event store — `MonitoringTests.DatabasePage_ReadsTheServer_TheTables_AndTheEventStore`
- [x] The migrator creates the tables and a second run finds nothing — `MigratorTests.Migrator_OnAnEmptyDatabase_CreatesTheSchema_AndASecondRunFindsNothing`
- [x] Browser: status counts the answers, lists a 404, the period changes the chart — `tests/Halcyon.E2E/SystemPagesTests.cs::Status_CountsTheAnswers_AndListsA404`
- [x] Browser: the log is shown and filtered — `SystemPagesTests::Logs_ShowTheApplicationLog_AndFilterIt`
- [x] Browser: database and queues pages, the cleanup on demand — `SystemPagesTests::Database_And_Queues_ShowTheirState_AndTheCleanupRunsOnDemand`
- [x] Signed-out visit goes to sign-in — `SystemPagesTests::SignedOut_GoesToSignIn`

## Decisions
- ADR-0011: plain tables, a bounded queue and COPY, retention by a hosted service.
- The Kolejki page shows what Halcyon really queues (memory buffers and background jobs), not a broker it does not have.
- The path is stored without the query string: a return address or a search can carry personal data.

## Notes
- **Defect found and fixed while building:** two hosts in one process (the integration tests) shared Serilog's static
  logger, so one host's log landed in another host's queue and database. `AddSerilog(..., preserveStaticLogger: true)`.
