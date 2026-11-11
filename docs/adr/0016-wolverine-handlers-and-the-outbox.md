# ADR-0016: Ticket commands go through Wolverine handlers; mail leaves through Marten's transactional outbox

- **Status:** Accepted - supersedes ADR-0001's "no message bus, no Wolverine and no background worker"
- **Date:** 2026-11-11
- **Evidence:** `src/Halcyon.App/Messaging/WolverineSetup.cs`, `src/Halcyon.App/Features/Tickets/*`,
  `src/Halcyon.App/Notifications/*`, `src/Halcyon.Database/DatabaseServices.cs`, `Dockerfile`
- **Specs:** 013-email-notifications (and every spec that changes a ticket: 002, 003, 004, 005, 008)

## Context
ADR-0001 left Wolverine out: no messaging, no outbox needed, and named the trigger to revisit - notifications that must
outlive a request. Spec 013 is that trigger. A mail sent from the request itself can leave for a change that is then
rolled back, or be lost when the process stops after the save; the fix is a transactional outbox, and the same machinery
schedules the deadline warnings durably. Ambit (`ambit.es`) runs exactly this: Marten + Wolverine, static handlers,
`AutoApplyTransactions`, cascading `OutgoingMessages`, code generated in the image.

## Decision
- **Wolverine 6.62 over Marten 9.58** (the versions Ambit runs). `IntegrateWithWolverine`: Marten's sessions enlist in
  the outbox; inbox, outbox and scheduled messages live in the schema `wolverine` on the same data source.
- **Every ticket operation is a command + a static `…Handler`** (`TakeTicketCommand` / `TakeTicketHandler`, ...), found
  by Wolverine's convention and called with `InvokeAsync`. `Policies.AutoApplyTransactions()` commits the handler's
  session - events, inline projection, outbox - in one transaction. The typed classes the pages use (`TakeTicket`, ...)
  translate a click into a command, as Ambit's endpoints translate a request.
- **The handler locks the ticket's stream** (`FetchForExclusiveWriting`, what `ConcurrencyStyle.Exclusive` does) and
  returns `(event, OutgoingMessages)`. Not `[AggregateHandler]`: measured on 6.62, it appends what it returns and gives
  the caller nothing back, and the pages need the event. The domain's decisions are unchanged (decide, then fold).
- **Mail is a message** (`ITicketMail`) routed to one durable local queue `mail`; deadline mails are scheduled
  (`Delay`). The mail handlers compose the mail when it is due and send it at most once (`SentMail` claim, released on
  failure); a policy scoped to the mail handlers retries transient failures (1 s, 5 s, 15 s) and dead-letters permanent
  ones. The ticket commands keep Wolverine's defaults.
- **One instance** (ADR-0001): `Durability.Mode = Solo` by default; `Messaging__Durability=Balanced` when several
  instances share the database. No RabbitMQ and no separate worker: the queue is in PostgreSQL.
- **Production shape**: the image runs `codegen write` and builds with `-p:WolverineStaticCode=true` - generated
  handler code compiled in, `TypeLoadMode.Static`, no Roslyn in the image (Ambit's API grew into an OOM kill with
  runtime compilation). `dotnet run` and the tests compile at runtime. Handler dependencies are registered by type or
  as instances, never by a factory lambda (it breaks `codegen write`). The migrator builds Wolverine's tables
  (`IMessageStore.Admin.MigrateAsync`, explicitly CreateOrUpdate); a release host builds nothing (ADR-0002).

## Alternatives considered
- **Send the mail from the page after the save**: lost on a crash between the two, sent for nothing when a later step
  fails. Rejected.
- **A hand-written outbox table and a `BackgroundService`**: the same thing, less tested, plus scheduling and retries to
  write. Rejected.
- **RabbitMQ and a notification worker, as in Ambit**: Ambit has many modules and several processes; Halcyon has one.
  Later, if the mail needs its own process.
- **`[AggregateHandler]`**: see the decision; revisit when Wolverine returns a response from the aggregate workflow.

## Consequences
- The write path has one more hop (the bus) and generated code; a handler's dependency must be resolvable by Wolverine's
  codegen. `codegen write` fails the image build where the app would fail at runtime.
- Each host holds a few connections for the durability agent; the test PostgreSQL allows 500 connections, as the test
  classes run in parallel.
- An empty database without Wolverine's tables stops the host at start - one more reason the migrator runs first.
- `IMessageBus` is scoped: singletons use `new MessageBus(IWolverineRuntime)` per call.

## Revisit when
- Several instances run (`Balanced`), or the mail needs its own process (then RabbitMQ, as Ambit).
- Wolverine's aggregate workflow can answer `InvokeAsync<TEvent>` with the appended event.
