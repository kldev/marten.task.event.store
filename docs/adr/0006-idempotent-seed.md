# ADR-0006: The seed inserts what is missing by a stable key and never overwrites

- **Status:** Accepted
- **Date:** 2026-11-03
- **Evidence:** `src/Halcyon.Database/Seed/*`, `src/Halcyon.DbMigrator/Program.cs`
- **Specs:** 007-database-and-seed

## Context
The owner wants demo data that can be run again safely: "when the seed changes, it does not make new changes".

## Decision
- Every seeded record has a stable key: agent and person = e-mail (`@company.io`), ticket = a fixed `Guid`.
  Ids are derived deterministically from the key (UUID v5-style hash), so the same key always gets the same id.
- **Insert if missing, never update.** Running twice changes nothing; a changed definition of an existing record does
  not touch the database; only new keys are added. A ticket stream is started only if it does not exist.
- The seed is a command (`Halcyon.DbMigrator seed`, a compose step), never part of the app's startup.
- 10 agents, 40 people (10 of them recruiters, added with spec 008), tickets in every state (pool, in progress, done, done late, overdue, handed over,
  with comments, incognito). Ticket dates are relative to the moment of the first run and then frozen. A "due in
  under an hour" ticket goes stale after the first run, so the e2e tests create their own instead of relying on it.

## Alternatives considered
- **Upsert (make the database match the seed):** would overwrite what agents changed. Rejected by the owner.

## Consequences
- Fixing a typo in a seeded name needs a manual change or a fresh database.

## Revisit when
- Real data replaces the demo data (then the seed is not run at all).
