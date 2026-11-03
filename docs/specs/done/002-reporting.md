# 002 · Reporting a ticket (by an agent or by the requester)

**Status:** done · **Built:** 2026-11-03 → 2026-11-03

## Why
Requests arrive by phone or chat (an agent writes them down) or from the requester directly. Either way they must
land in the shared pool with a priority and a clock running.

## What it promises
- A ticket has title, description, category, subcategory of that category, priority, report date and requester.
- Requester = a person from the list **or** incognito. Never both, never neither. An agent cannot be the requester.
- The requester sets the priority (required). An agent registering on someone's behalf enters it.
- An agent picks the report date and time with the custom date picker; it can be back-dated up to 30 days, not more
  than 5 minutes into the future. The public form uses the moment of sending.
- The new ticket is in the pool, with no assignee, deadline = report date + SLA.
- The public page `/zglos` needs no sign-in, lets a person find themselves in the list, shows the ticket number after
  sending, and gives no access to any other page.
- Category names: "Zgłoszenie do IT", "Zgłoszenie do działu Prawnego", "Zgłoszenie do Marketingu"; picking a category
  narrows the subcategories; a retired subcategory is not offered but old tickets still show it.

## Surface
- Domain: `Ticket.Report`, `CategoryCatalog`, `Requester`. App: `Features/Tickets/ReportTicket.cs`,
  `Pages/NewTicket.razor`, `Pages/Public/Report.razor`, `Components/Ui/DateTimePicker.razor`.

## Out of scope
- Attachments, requesters following their tickets (not yet).

## Acceptance criteria
- [x] Valid report → `InPool`, deadline from priority — `tests/Halcyon.Domain.Tests/ReportingTests.cs`
- [x] Future date (> 5 min) refused, back-dated > 30 days refused — `ReportingTests`
- [x] Subcategory of another category / unknown / retired refused — `ReportingTests`, `CategoryCatalogTests`
- [x] Empty or too long title/description refused, whitespace trimmed — `ReportingTests`
- [x] Requester: person xor incognito; unknown person and agent-as-person refused — `ReportingTests`, `IntegrationTests/TicketWorkflowTests.cs`
- [x] Agent report and public report land in the pool view — `TicketWorkflowTests`
- [x] Browser: date picked in the calendar, cascade, live deadline preview, ticket in the pool — e2e `ReportTicketTests`
- [x] Browser: future days disabled in the picker — e2e `ReportTicketTests`
- [x] Browser: public form as a person and as incognito, no way into the pool — e2e `PublicReportTests`

## Notes
- The form's submit callback was a plain `Func`, so the public page never re-rendered to show the ticket number;
  it is an `EventCallback` now (found by e2e).

## Decisions
- ADR-0003 (categories as code), ADR-0004 (date rules), ADR-0005 (public form).
