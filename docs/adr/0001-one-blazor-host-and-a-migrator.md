# ADR-0001: One Blazor Server host with the logic inside it, and a separate schema migrator

- **Status:** Accepted
- **Date:** 2026-11-03
- **Evidence:** `src/Halcyon.App`, `src/Halcyon.DbMigrator`
- **Specs:** all

## Context
Halcyon is a small internal tool: agents take service requests (IT, legal, marketing) from a shared pool. One
organization, a handful of agents, up to a few dozen requesters. It has to be simple to run and to change, but its
database setup has to be production-grade (schema changes are a release step, not a side effect of starting the app).

## Decision
- **One process serves everything:** `Halcyon.App` is an ASP.NET Core host with Blazor (Interactive Server). The
  pages call application handlers (`Features/<Area>/<Operation>.cs`) in-process through DI. There is no separate
  HTTP API, no message bus, no Wolverine and no background worker.
- **Projects:** `Halcyon.Domain` (aggregates, events, rules, no Marten and no ASP.NET reference),
  `Halcyon.Database` (the one `StoreOptions` description shared by the host and the migrator, the migration runner),
  `Halcyon.App` (host, features, UI), `Halcyon.DbMigrator` (console: `apply`, `--dry-run`, `seed`).
- **Single tenant:** no organization or tenant id anywhere (not a multi-tenant product).
- Code, comments, commits and ADRs in English; the UI in Polish.

## Alternatives considered
- **API + separate front (WASM or JS):** two deployables and an HTTP contract for a tool with a few users. Rejected.
- **Wolverine for handlers:** useful for messaging and the outbox, neither is needed here. Rejected; plain classes.
- **Modular monolith like the HR reference:** one bounded context does not need module boundaries.

## Consequences
- A Blazor circuit holds server memory per open tab - fine for tens of users.
- Adding an external integration later (mail, chat) means adding a background process; the handlers stay as they are.

## Revisit when
- Someone outside the company needs an API, or the requesters need their own front.
- Notifications that must outlive a request (mail, SMS) are required.
