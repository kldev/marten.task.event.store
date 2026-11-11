# 013 · E-mail notifications over Wolverine and Marten's outbox

**Status:** done · **Built:** 2026-11-11 → 2026-11-11

## Why
A requester who used the public form hears nothing until they come back and ask; an agent who was handed a ticket
learns it only by opening the list; a critical ticket nobody has taken runs out in silence outside office hours. The
deadline alerts are UI only (ADR-0004). Mail closes the gap - but a mail must never leave for a change that was rolled
back, and never be lost when the change was saved.

## What it promises
- **The requester** (a person from the list, not incognito) gets a confirmation with the number, the topic, the priority
  and the deadline, and a mail when the ticket is resolved. No link: requesters do not sign in (ADR-0005).
- **The agent a ticket is handed over to** gets a mail with who handed it, why, the deadline and a link to the ticket.
- **An hour before the deadline** (ADR-0004's "due soon") and **at the deadline**, the agent the ticket is with gets a
  warning and an alert; a ticket still in the pool goes to `Mail:PoolAddress` when it is set. A ticket reported past its
  deadline gets its alert at once. A priority change schedules new mails; the old ones see the deadline is no longer the
  ticket's and stay quiet. A resolved ticket gets no more deadline mail.
- **Exactly the saved changes cause mail**: the mails go into Wolverine's outbox in the same transaction as the events;
  a refused or failed command sends nothing, a saved one loses nothing - not on a crash, not on a restart (the queue and
  the scheduled mails are in PostgreSQL).
- **At most once per message**: a redelivered message finds its claim (`SentMail`) and stays quiet; a failed send
  releases the claim and is tried again after 1 s, 5 s, 15 s; a malformed address or a 5xx refusal goes to the dead
  letters at once.
- **Production configuration**: SMTP server, sender, the app's address, the pool's mailbox from `Mail__*`; a wrong set
  stops the host at start naming the setting. Without `Mail__Provider=Smtp` the mails go to the log only.
- **Every ticket operation is a command with a static Wolverine handler** (the way Ambit's are), called through the bus;
  the handler locks the ticket's stream, so two clicks at once are served one after the other.

## Surface
- App: `Features/Tickets/*` (command record + `…Handler` per operation, `TicketCommands`, `TicketStreams`),
  `Notifications/*` (messages, `TicketMailPlan`, `MailComposer`, `MailDelivery`, SMTP/log senders, `MailOptions`, the
  four mail handlers), `Messaging/*` (`WolverineSetup`, `MessagingOptions`, `MailFailurePolicies`), `Program` (`codegen`).
- Database: `IntegrateWithWolverine` (schema `wolverine`), `SentMail`, `SchemaMigrator` migrates the message store,
  `AddHalcyonMigration`.
- Ops: `Dockerfile` (`codegen write`, `WolverineStaticCode`), `docker-compose.yml` (Mailpit, `Mail__*`), `.env-sample`.

## Out of scope
- Mail in the requester's language (not yet: requesters have no language setting; the mails are Polish, ADR-0012).
- Replies by mail turned into comments (not yet).
- A separate notification worker and RabbitMQ, as in Ambit (not while there is one instance - ADR-0016).
- Business-hours deadlines (ADR-0004).

## Acceptance criteria
- [x] Confirmation to the requester: number, deadline in Warsaw time, priority, no link — test: `tests/Halcyon.IntegrationTests/NotificationTests.cs::AReport_ForAPerson_SendsTheConfirmation_WithTheNumberAndTheDeadline`
- [x] No confirmation for an incognito report — test: `NotificationTests::AnIncognitoReport_PlansNoConfirmation`
- [x] Resolution tells the requester — test: `NotificationTests::Resolving_TellsTheRequester`
- [x] Handover tells the new agent, with who, why and a link — test: `NotificationTests::AHandover_TellsTheNewAgent_WithWhoWhyAndALink`
- [x] A ticket past its deadline alerts the pool's mailbox at once — test: `NotificationTests::ATicketReportedPastItsDeadline_AlertsThePoolAtOnce`
- [x] Warning and alert scheduled in the database at deadline − 1 h and at the deadline, in the one mail queue — test: `NotificationTests::ATicketWithTimeLeft_HasItsWarningAndAlertScheduled_InTheDatabase`
- [x] A priority change sends the new warning; the old deadline's mail stays quiet — test: `NotificationTests::APriorityChange_SchedulesNewMails_AndTheOldOnesStayQuiet`
- [x] No deadline mail for a resolved ticket — test: `NotificationTests::ADeadlineMail_ForAResolvedTicket_StaysQuiet`
- [x] The same message twice leaves once — test: `NotificationTests::TheSameMail_DeliveredTwice_LeavesOnce`
- [x] A flaky SMTP server is retried and the mail leaves once — test: `NotificationTests::AFlakySmtpServer_IsTriedAgain_AndTheMailLeavesOnce`
- [x] A refused command sends nothing — test: `NotificationTests::ACommandThatFails_SendsNothing`; a report made again after a number clash sends one confirmation per ticket — test: `SlaAndNumberTests::AReportThatMeetsAGivenNumber_IsMadeAgainUnderANewId`
- [x] The mails wait in PostgreSQL — test: `NotificationTests::TheMailsWait_InTheDatabase_NotInMemory`
- [x] Real SMTP: Mailpit receives the confirmation from the configured sender — test: `NotificationTests.cs::MailpitTests::TheConfirmation_ArrivesAtTheSmtpServer_FromTheConfiguredSender`
- [x] A wrong mail setting stops the host, naming it — test: `MailpitTests::AWrongMailSetting_StopsTheHost_NamingIt`
- [x] The migrator builds Wolverine's tables; a release host does not start on a database without them — test: `MigratorTests` (all)
- [x] Two agents at once on the stream lock: exactly one wins, the other hears who — test: `ConcurrencyTests` (all)
- [x] Browser: a public report brings the requester the number they were shown — test: `tests/Halcyon.E2E/NotificationTests.cs::APublicReport_SendsTheRequesterTheConfirmation_WithTheNumberTheyWereShown`
- [x] Browser: a handover in the page mails the other agent with the link — test: `E2E NotificationTests::AHandoverInThePage_SendsTheOtherAgentAMail_WithTheLink`
- [x] Browser: resolving in the page tells the requester — test: `E2E NotificationTests::ResolvingInThePage_TellsTheRequester`

## Decisions
- [ADR-0016](../../adr/0016-wolverine-handlers-and-the-outbox.md): Wolverine handlers, the outbox over Marten, the
  durable mail queue, static code in the image.
- The mails are composed when they are sent, from the ticket as it is then: a scheduled warning reads the current
  assignee, and a changed deadline needs no cancellation.
- Deadline mails are delayed by the producing host's clock (`OutgoingMessages.Delay`), so they follow the same
  `TimeProvider` as the deadline itself.

## Notes
- Measured with a spike on PostgreSQL before the design: in Wolverine 6.62 an `[AggregateHandler]` appends whatever it
  returns and gives the caller nothing back (`InvokeAsync<TEvent>` returns null, `RespondToSender` too). The pages need
  the event (the "Niski → Krytyczny" notice, "zrealizowane po terminie"), so the handlers take `IDocumentSession` and
  lock the stream themselves with `FetchForExclusiveWriting` - what `ConcurrencyStyle.Exclusive` does - and return
  `(event, OutgoingMessages)`. Same pipeline, transaction and outbox; the response is the event.
- `PublishMessage<ITicketMail>()` matches the interface only; the records went to a local queue each until the rule
  became `MessagesImplementing<ITicketMail>()` - the scheduled-mail test caught it.
