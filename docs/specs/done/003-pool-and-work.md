# 003 · Pool, taking, handing over, returning, resolving

**Status:** done · **Built:** 2026-11-03 → 2026-11-03

## Why
Work must not be done twice or dropped: one agent owns a ticket at a time, and ownership can move.

## What it promises
- Any agent takes a ticket from the pool; it becomes theirs (`InProgress`).
- Two agents taking the same ticket at once: one wins, the other is told who was faster.
- Only the assignee resolves the ticket (`Done`); resolving twice is refused.
- The assignee hands the ticket to another agent (optional reason); not to themselves, not to an unknown agent.
- The assignee returns the ticket to the pool (optional reason); anyone can take it again.
- Handover and return never move the deadline.
- Concurrent handover / return / resolve on one ticket: one wins.
- Lists: pool, my tickets, all tickets, with filters (category, subcategory, priority, overdue, due soon).

## Surface
- Domain: `Ticket.Take/HandOver/ReturnToPool/Resolve`. App: `Features/Tickets/*`, `Pages/Pool.razor`,
  `Pages/MyTickets.razor`, `Pages/AllTickets.razor`, `Pages/TicketDetails.razor`.

## Acceptance criteria
- [x] Take / not-in-pool refused — `tests/Halcyon.Domain.Tests/WorkflowTests.cs`
- [x] Resolve by assignee only, twice refused, late flag — `WorkflowTests`
- [x] Handover rules and unchanged deadline — `WorkflowTests`
- [x] Return rules and unchanged deadline — `WorkflowTests`
- [x] Concurrent take: exactly one wins — `tests/Halcyon.IntegrationTests/ConcurrencyTests.cs`
- [x] Concurrent handover vs resolve: exactly one wins — `ConcurrencyTests`
- [x] Pool / mine / all lists and filters — `IntegrationTests/TicketQueriesTests.cs`
- [x] Browser: take → mine → resolve — e2e `WorkTests`
- [x] Browser: two agents take the same ticket → the second sees the message — e2e `WorkTests`
- [x] Browser: handover moves the ticket to the other agent; return puts it back in the pool — e2e `WorkTests`

## Decisions
- ADR-0003 (optimistic concurrency per stream).
