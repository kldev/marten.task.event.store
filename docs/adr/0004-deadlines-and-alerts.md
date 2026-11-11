# ADR-0004: Deadlines are calendar time from the report date; alert state is computed when read

- **Status:** Accepted
- **Date:** 2026-11-03
- **Evidence:** `src/Halcyon.Domain/Tickets/Sla.cs`, `DeadlineState.cs`, `TimeLeftText.cs`
- **Specs:** 004-deadlines-and-alerts

## Context
Priorities give a time to resolve: Critical 3 h, High 8 h, Medium 3 days, Low 1 week. Agents must see what is about
to run out and what already has, without reloading the page.

## Decision
- **Deadline = report date + SLA of the current priority**, in absolute (UTC) calendar time, 24/7, to the minute.
  A priority change recomputes the deadline **from the report date**, never from the moment of the change. Handover
  and return to the pool do not touch it.
- **Report date rules:** an agent may back-date a report (a phone call this morning) up to 30 days; a date more than
  5 minutes in the future is refused. The public form uses the moment of sending.
- **Three states, computed by the domain from `TimeProvider`** for an unresolved ticket:
  `OnTime` (more than 60 minutes left), `DueSoon` (less than 60 minutes left, warning), `Overdue` (now >= deadline,
  alert). Exactly 60 minutes left is still `OnTime`; the deadline instant itself is `Overdue`. A resolved ticket has no
  state; a ticket resolved after its deadline keeps the flag `ResolvedLate` (stored in the completion event).
- **Nothing runs in the background.** States are computed at read time; the Blazor layout ticks every 30 s and
  re-renders, so a warning turns into an alert by itself.
- **Formatting (`TimeLeftText`)**, rounding down to whole minutes: over 24 h "2 dni 5 h", 1-24 h "5 h 20 min",
  under 1 h "45 min" (warning style), overdue "po terminie o 2 h 10 min" (alert style). Colors are never the only
  signal: each state also has its own icon and text.
- Times are stored in UTC and shown in `Europe/Warsaw`; because deadlines are absolute, a DST change does not move them.
- Alerts are UI only (no mail).

## Alternatives considered
- **Business hours SLA:** not requested; needs a holiday calendar. Rejected for now.
- **A scheduled job writing "overdue" events:** a fact that is only a function of time would need a worker and
  could lag. Rejected.

## Consequences
- Since ADR-0013 the windows come from the configuration (defaults as above) and the deadline is stored in the event
  that gives it; events stored before it are read with the default windows.
- Lists filter "overdue / due soon" on the indexed `Deadline` column against "now" at query time.

## Revisit when
- Mail or chat notifications are required (then a background process emits them).
- SLA must exclude nights, weekends or holidays.
