# ADR-0002: Marten in production mode - schema only through the migrator, one pool, inline projections

- **Status:** Accepted (Marten 9.58.0, with Wolverine since ADR-0016)
- **Date:** 2026-11-03
- **Evidence:** `src/Halcyon.Database/HalcyonStore.cs`, `DatabaseStartup.cs`, `src/Halcyon.DbMigrator/Program.cs`
- **Specs:** 007-database-and-seed

## Context
Marten's development defaults create and patch tables on the fly (`AutoCreate.All`) the first time a document type is
used. That is convenient on a laptop and dangerous anywhere else: a release silently alters a shared database, two
replicas race on DDL, and a failure surfaces as a broken request instead of a failed deployment. The HR reference
(ADR 0055 there) runs Marten 9.53 with the schema applied by a migrator, one data source per process and an explicit
event store configuration; this app follows it, scaled down.

## Decision
- **The store is described once** (`HalcyonStore.Configure(StoreOptions)`), used by the host and the migrator.
- **Schema changes only through `Halcyon.DbMigrator`.** It applies the store's whole patch with
  `ApplyAllConfiguredChangesToDatabaseAsync(AutoCreate.CreateOrUpdate)`, re-checks that nothing is left, and with
  `--dry-run` prints the SQL and changes nothing. Its command timeout is `Database:MigrationCommandTimeoutSeconds`
  (default 900 s; Weasel sends a patch as one command).
- **The host never changes the schema outside local environments.** One switch, `Database:ApplyChangesOnStartup`:
  when unset it is on in `Development` and `Testing`, off elsewhere. Off means `AutoCreateSchemaObjects =
  AutoCreate.None` and a check at startup: if the store has pending changes the host refuses to start and names the
  migrator.
- **One `NpgsqlDataSource` per process**, registered in DI and handed to Marten with `.UseNpgsqlDataSource()`.
- **Lightweight sessions** (no identity map for documents), events in schema `events`, documents in `halcyon`, stream
  identity `Guid`, `EventAppendMode.Quick`, System.Text.Json with enums as strings.
- **Every projection is inline** (`TicketView`, written in the same transaction as the events). There is no async
  daemon, so `EnableAdvancedAsyncTracking` and event skipping are not turned on - they only matter for a daemon.
- **No code generation step.** Marten 9 removed runtime code generation (no Roslyn, no `codegen write`, no
  `Internal/Generated`); the dispatchers are built at compile time or written by hand. Our projections override
  `Evolve` explicitly, so the domain project needs no Marten source generator.
- At startup the host builds the schema patch of every configured type (to apply it locally or to check it in a release),
  so a bad document mapping fails the start, not the first request. (`Storage.BuildAllMappings()` from Marten's docs is
  not public in 9.57.)

## Alternatives considered
- **`ApplyAllDatabaseChangesOnStartup()` in the host:** convenient, but every replica would race on DDL. Rejected
  outside local environments.
- **Async projections with the daemon:** needed for heavy read models; ours is one small document per ticket and must
  be consistent right after a click. Rejected.
- **Liquibase / SQL migrations:** Marten owns its schema; a second source of truth would drift.

## Consequences
- A release runs the migrator first (compose: one-shot service before the app).
- Switching to async projections later requires the daemon settings from the HR reference (tracking, skipping,
  health checks).

## Revisit when
- A projection becomes expensive enough to move off the write path.
- More than one host process writes to the store.
