# 007 · Production database setup and the idempotent seed

**Status:** done · **Built:** 2026-11-03 → 2026-11-03

## Why
The database must change only on purpose, and the demo data must be safe to load again.

## What it promises
- The migrator creates the schema on an empty database; a second run (and `--dry-run` after it) finds nothing.
- A host with `ApplyChangesOnStartup=false` on a database that is behind refuses to start and names the migrator.
- Marten and the app share one `NpgsqlDataSource`.
- `/health` (alive) and `/health/ready` (database reachable, schema current).
- The seed adds 10 agents, 40 people (30, plus 10 recruiters since spec 008) and tickets in every state; running it twice adds nothing; a manually changed
  record stays as it is; a new record in the definition is the only thing added.

## Acceptance criteria
- [x] Empty database → schema; second run nothing pending — `tests/Halcyon.IntegrationTests/MigratorTests.cs`
- [x] Database behind → host refuses to start — `MigratorTests`
- [x] One data source — `MigratorTests`
- [x] Readiness reports the database (and is unhealthy on a schema that is behind) — `MigratorTests`
- [x] Seed twice → same counts of documents and events — `SeedTests`
- [x] Changed record preserved — `SeedTests`
- [x] Only the new key added — `SeedTests`

## Decisions
- ADR-0002, ADR-0006.
