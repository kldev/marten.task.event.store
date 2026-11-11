# ADR-0014: A ticket's number is unique by a unique index; a report that meets a given number is made again

- **Status:** Accepted
- **Date:** 2026-11-11
- **Evidence:** `src/Halcyon.Database/HalcyonStore.cs` (`uidx_ticket_number`, `IsNumberTaken`),
  `src/Halcyon.App/Features/Tickets/ReportTicket.cs` (`TicketIds`, the retry)
- **Specs:** 012-ported-from-ambit

## Context
The number a requester is given ("HAL-" and 8 hex digits of the id) is short on purpose, but 8 hex digits are 2^32
values: with tens of thousands of tickets two of them meeting is no longer unthinkable, and nothing stopped it - a
search by number or a linked ticket in the report data would then find the wrong one. Ambit's Service Desk keeps its
numbers unique with a unique index (a reservation document) written in the same transaction, so a race fails the
slower save instead of giving one number twice.

## Decision
- `TicketView.Number` is a duplicated column with the unique index `uidx_ticket_number`, written inline with the
  events (one transaction, ADR-0002).
- `ReportTicket` makes a report under a new id when the save fails on that index (`HalcyonStore.IsNumberTaken`), at most
  3 attempts; the failed attempt leaves nothing (one transaction; its files are removed, ADR-0008).
- The number's format stays as it is: numbers already given, the screenshots and the report data keep working.
- Where the id comes from is a service (`TicketIds`), so a test can make two numbers meet.

## Alternatives considered
- **A sequence (`HAL-2026-00042`, as Ambit)**: readable and gap-free, but a new format for numbers already given and
  stored in report data; not worth it for this size.
- **Longer numbers**: fewer meetings, never none, and harder to read over the phone.

## Consequences
- The migrator creates the index; on a database that already holds two equal numbers it fails and names the index -
  then one of them must be given a new number by hand first.

## Revisit when
- Requesters need numbers in order, per year or per team.
