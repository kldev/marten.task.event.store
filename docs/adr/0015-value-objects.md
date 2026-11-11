# ADR-0015: Values with rules are value objects, written the way Ambit writes them

- **Status:** Accepted
- **Date:** 2026-11-11
- **Evidence:** `src/Halcyon.Domain/ValueObjects/` - `TicketTitle`, `TicketDescription`, `CommentText`, `Reason`,
  `Email`, `PhoneNumber`, `Pesel`, `Iban`, `Amount`, `TicketNumber`, `AttachmentFileName`
- **Specs:** 002, 005, 008 (the rules themselves are unchanged)

## Context
The rules of single values were spread over helpers: `TicketLimits.Required/Optional` with the length constants,
`ReportFields.IsValidPesel/NormalizeIban/ParseAmount` and a switch for e-mails and ticket numbers, `Emails.Normalize`,
`Ticket.NumberOf`, the file name inside `AttachmentRules.Check`. The same check could be written twice (the ticket
number pattern), and a phone number or an e-mail of the seed was never checked at all. Ambit (`ambit.es`) keeps such
values as value objects (`Email`, `PersonPhone`, `TaskTitle`, `BankAccount`, ...).

## Decision
The same shape as Ambit's:
- `sealed record` with a private constructor and `Value` (and what belongs to the value: `Email.Normalized`,
  `PhoneNumber.Dial`, `Amount.Stored`, `AttachmentFileName.ContentType`); equality by value; `ToString()` is the value.
- `TryCreate(...)` returns `(value, error)` - never both - after trimming and normalizing; `Create(...)` throws the same
  message. The messages are public constants (or functions of a field's label), so tests assert on them.
- `FromStored(...)` builds one from an event without the rules, only where a fold needs it (the aggregate's
  `Title` and `Description`): a stored event is a fact and folding never refuses (ADR-0003).
- Events and documents keep primitives (strings): stored data and the event schema do not change.

Halcyon's own differences, on purpose:
- `Create` throws `DomainException` (Halcyon's refusal), not Ambit's `InValidValueException`.
- The messages stay the Polish ones word for word: the UI translates them by exact text and pattern (ADR-0012).
- One refusal at a time, as before: the form shows one message; Ambit's handlers collect a list.
- One folder, `Halcyon.Domain/ValueObjects`: Halcyon has one domain, Ambit has a SharedKernel and modules.

## Alternatives considered
- **Keep the static helpers**: less code, but nothing says which strings were checked, and rules drift apart.
- **Value objects in the events** (serialized records): a new event schema for no gain; Ambit does not do it either.

## Consequences
- The seed goes through `Email` and `PhoneNumber`: a typo in the seed data stops it, naming the value.
- A Windows path ("C:\\Users\\x.png") gives the file name on Linux too (`AttachmentFileName` cuts at both separators).
- A new value with a rule gets a value object here, with its tests in `tests/Halcyon.Domain.Tests/ValueObjectTests.cs`.

## Revisit when
- A form needs every refusal at once (then collect the `TryCreate` errors, as Ambit's handlers do).
