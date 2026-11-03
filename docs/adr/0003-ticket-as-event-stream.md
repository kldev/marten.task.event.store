# ADR-0003: A ticket is an event stream; agents and requesters are documents; categories are code

- **Status:** Accepted
- **Date:** 2026-11-03
- **Evidence:** `src/Halcyon.Domain/Tickets/*`, `src/Halcyon.App/Infrastructure/TicketProjections.cs`
- **Specs:** 002-reporting, 003-pool-and-work, 005-comments

## Context
A ticket's history (who reported it, who took it, handovers, priority changes, returns to the pool, comments) is
what the agents look at, and the overdue rules depend on the order of those facts.

## Decision
- **One stream per ticket.** Events: `TicketReported`, `TicketTaken`, `TicketHandedOver`, `TicketReturnedToPool`,
  `TicketPriorityChanged`, `TicketResolved`, `TicketCommented`. Each carries the actor's name as it was then
  (a snapshot), so the timeline never changes when a name does.
- **Write side:** the `Ticket` aggregate (pure C#) folds its events (`Ticket.Evolve`, private `On` methods - public `Apply` methods would clash with the projection's explicit `Evolve` in Marten 9). Decisions are methods that
  check the rules and return the event to append; folding never validates - a stored event is a fact.
  Handlers use `FetchForWriting<Ticket>`; Marten's optimistic check makes the loser of two concurrent writes fail
  with a `ConcurrencyException`, shown to the agent as "someone was faster, refresh".
- **Read side:** `TicketView`, an inline single-stream projection with indexed columns for the lists (status,
  assignee, category, priority, deadline). The timeline is read straight from the stream.
- **Agents and requesters (people)** are plain documents with a unique index on the normalized e-mail. They have no
  status and no workflow, so event sourcing them buys nothing.
- **Categories and subcategories are code** (`CategoryCatalog`, stable keys like `it.hardware`, Polish names). They
  change with a release, the domain validates against them, and a subcategory can be retired (`Active = false`)
  without breaking stored tickets. The seed does not write them.
- A ticket has a short public number (`HAL-` + 8 hex of its id) shown to the requester after reporting.

## Alternatives considered
- **Categories as documents editable at runtime:** nobody asked for an editor, and a rule that depends on data
  needs that data in every test. Rejected for now.
- **Status-only document for tickets (CRUD):** loses the history the agents need and the concurrency guarantee
  per ticket.

## Consequences
- Renaming a category is a code change; keys never change.
- A new event type needs a case in `Ticket.Evolve` and in `TicketViewProjection`.

## Revisit when
- The business wants to manage categories without a release.
