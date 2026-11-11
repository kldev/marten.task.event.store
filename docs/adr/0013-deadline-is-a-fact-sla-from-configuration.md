# ADR-0013: The deadline is a fact in the ticket's events; the SLA windows come from the configuration

- **Status:** Accepted
- **Date:** 2026-11-11
- **Evidence:** `src/Halcyon.Domain/Tickets/Priority.cs` (`SlaPolicy`, `SlaWindowText`), `TicketEvents.cs`
  (`Deadline` on `TicketReported` and `TicketPriorityChanged`), `src/Halcyon.App/Features/Tickets/SlaOptions.cs`
- **Specs:** 012-ported-from-ambit, 004-deadlines-and-alerts

## Context
Until now the deadline was computed again on every fold from the priority and a table in code (`Sla`). Ambit's
Service Desk (`DuePolicy`, `ServiceRequestFiled.DueAt`) does the opposite: the windows come from the configuration and
the due time is written into the event. In Halcyon a change of the windows in code would quietly move the deadline of
every ticket already reported - and turn a ticket resolved on time into one that looks late in the charts, while its
`TicketResolved.Late` says otherwise.

## Decision
- **The windows are configuration**: `Sla:CriticalHours`, `Sla:HighHours`, `Sla:MediumHours`, `Sla:LowHours`
  (env `Sla__*`), defaults 3 / 8 / 72 / 168 - the owner's 3 h / 8 h / 3 days / 1 week (`SlaPolicy.Default`). Each must
  be positive and a more urgent priority must not have more time (Critical <= High <= Medium <= Low); otherwise the host
  refuses to start and names the reason (`ValidateOnStart`).
- **The deadline is stored in the event** that gives it: `TicketReported.Deadline` and `TicketPriorityChanged.Deadline`,
  still counted from the report date (ADR-0004). The aggregate and `TicketView` read it from there.
- **Events stored before this ADR** have no deadline (null); they are read with `SlaPolicy.Default`, the windows they
  were made under. No upcaster and no rewrite of the stream is needed.
- The seed (run by the migrator) uses the default windows.
- Every UI text that names a window ("3 dni", "5 days") is formatted from the configured policy.

## Alternatives considered
- **Keep the table in code**: the simplest, but an SLA agreed with the teams would need a release, and a changed table
  rewrites history.
- **Windows per category in the catalog**: not requested; the configuration is the smaller step and the event already
  carries the deadline, so it could follow without a migration.

## Consequences
- Changing `Sla__*` moves only the tickets reported or re-prioritised after the change.
- The charts' "SLA use" already reads `Deadline - ReportedAt` per ticket, so it stays true across a change; its label
  shows the current windows.

## Revisit when
- SLA per category or per team is needed.
- SLA must exclude nights, weekends or holidays (business hours).
