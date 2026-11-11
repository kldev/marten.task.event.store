# 004 · Deadlines, time left, warnings and alerts

**Status:** done · **Built:** 2026-11-03 → 2026-11-03

## Why
A critical ticket has 3 hours. Agents must see at a glance what is about to run out and what already has.

## What it promises
- SLA: Critical 3 h, High 8 h, Medium 3 days, Low 1 week; deadline = report date + SLA, 24/7. Since spec 012 these
  are the defaults of a configurable policy, and the deadline is stored in the event.
- An agent can change the priority of an unresolved ticket; the deadline is recomputed **from the report date**.
  Same priority = no change, no event. Resolved ticket: refused. The original priority stays in the history.
- Time left shown in hours: "2 dni 5 h", "5 h 20 min"; under 1 h "45 min" in the warning style.
- Overdue: a different alert - "po terminie o 2 h 10 min", other color and icon, own counter in the header.
- Both counters are on every page; a warning turns into an alert without a reload.
- A ticket resolved after its deadline is marked "resolved late" for good; resolved tickets show no alert.
- DST does not move a deadline.

## Surface
- Domain: `SlaPolicy` (spec 012), `DeadlineState`, `TimeLeftText`, `Ticket.ChangePriority`. App: `Components/Ui/TimeLeft.razor`,
  `Layout/AlertCounters.razor`.

## Acceptance criteria
- [x] SLA per priority — `tests/Halcyon.Domain.Tests/DeadlineTests.cs`
- [x] Boundaries 60:00 / 59:59 / 0 / past — `DeadlineTests`
- [x] DST crossing keeps the absolute deadline — `DeadlineTests`
- [x] Formats incl. 1 dzień 0 h, 23 h 59 min, 1 h 0 min, 0 min, overdue 1 min — `TimeLeftTextTests`
- [x] Priority change from report date, no-op, refused when done, raises to overdue, lowering clears — `PriorityTests`
- [x] Overdue / due-soon filters and counters — `IntegrationTests/TicketQueriesTests.cs`
- [x] Browser: overdue alert from the seed, disappears after resolving — e2e `AlertTests`
- [x] Browser: due-soon warning looks different from overdue — e2e `AlertTests`
- [x] Browser: priority change Low → Critical on a 4 h old ticket gives the overdue alert and a timeline entry — e2e `AlertTests`

## Decisions
- ADR-0004.
