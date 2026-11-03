# 005 · Comments and the timeline

**Status:** done · **Built:** 2026-11-03 → 2026-11-03

## Why
Whoever takes over a ticket needs to know what happened and what was said.

## What it promises
- Any signed-in agent comments on a ticket in any state, including resolved.
- Comments are immutable, chronological, with author and time; text is trimmed, not empty, at most 2000 characters,
  rendered as text (no HTML).
- The ticket page shows the timeline: reported (and how), taken, handed over, returned, priority changed, resolved.

## Acceptance criteria
- [x] Empty / whitespace / too long refused, trimmed — `tests/Halcyon.Domain.Tests/CommentTests.cs`
- [x] Comment stored and shown with author, also on a resolved ticket; an action on an unknown ticket is refused — `IntegrationTests/TicketWorkflowTests.cs`
- [x] Timeline lists every event in order — `TicketWorkflowTests`
- [x] Browser: add a comment, another agent sees it; `<script>` shown as text — e2e `CommentTests`

## Decisions
- ADR-0003.
