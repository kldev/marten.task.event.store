# ADR-0007: Three separate test levels - domain, integration on real PostgreSQL, browser e2e

- **Status:** Accepted
- **Date:** 2026-11-03
- **Evidence:** `tests/Halcyon.Domain.Tests`, `tests/Halcyon.IntegrationTests`, `tests/Halcyon.E2E`
- **Specs:** all

## Decision
- **`Halcyon.Domain.Tests`:** the aggregate, SLA, states, formatting and validation; no I/O, `FakeTimeProvider`.
- **`Halcyon.IntegrationTests`:** PostgreSQL 18 in **Testcontainers** (one container per test collection, a fresh
  database per test class). The schema is applied by the **migrator code** (the production path), then the host is
  started with `WebApplicationFactory` in `Testing` with `ApplyChangesOnStartup=false`. Covers handlers, concurrency,
  sign-in over HTTP, the seed, the "database behind" refusal, the one-pool rule.
- **`Halcyon.E2E`:** Playwright for .NET (Chromium) against the real host on Kestrel (random port) and its own
  Testcontainers database with the seed. Run separately (`scripts/test.sh e2e`); the browser must be installed once.
- Every acceptance criterion in `docs/specs` names its test.

## Consequences
- Docker is required for integration and e2e tests.

## Revisit when
- The suite gets slow enough to need a shared database.
