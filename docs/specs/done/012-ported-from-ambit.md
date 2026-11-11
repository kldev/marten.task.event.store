# 012 · Configurable SLA with the deadline in the events, and a unique ticket number

**Status:** done · **Built:** 2026-11-11 → 2026-11-11

## Why
Two solutions of Ambit's Service Desk fit Halcyon as it is. Ambit keeps the due time as a fact in the request's events
and takes the windows from the configuration (`DuePolicy`, `DueAt`); it keeps request numbers unique with a unique
index written in the same transaction. Halcyon recomputed every deadline from a table in code - a changed table would
move the past - and nothing stopped two tickets from getting one short number.

## What it promises
- The SLA windows can be set per deployment (`Sla__CriticalHours`, `__HighHours`, `__MediumHours`, `__LowHours`);
  without them the owner's 3 h / 8 h / 3 days / 1 week hold.
- Windows that make no sense (zero, or a more urgent priority with more time) stop the host with the reason.
- A report and a priority change store the deadline they gave; a later change of the windows does not move it.
- Tickets stored before keep the deadline of the default windows.
- The forms, the priority change and the charts name the configured windows, in Polish and English.
- No two tickets share a number; a report that meets a given number is made again under a new id, at most 3 times.

## Surface
- Domain: `SlaPolicy`, `SlaWindowText`, `Ticket.Report` / `ChangePriority` (`sla`), `Ticket.DeadlineOf`, `Deadline` in
  `TicketReported` and `TicketPriorityChanged`.
- Database: `TicketViewProjection` reads the stored deadline; unique index `uidx_ticket_number`,
  `HalcyonStore.IsNumberTaken`.
- App: `SlaOptions` (`AddHalcyonSla`), `ReportTicket` (`TicketIds`, retry), `ChangeTicketPriority`, `Tr.SlaText`;
  `ReportForm`, `TicketPage`, `SlaBars`, `ChartsPage`.

## Out of scope
- SLA per category or team; business hours (not yet).
- A new number format, e.g. a yearly sequence (not ever for numbers already given - ADR-0014).

## Acceptance criteria
- [x] Default windows and their words in both languages — test: `tests/Halcyon.Domain.Tests/SlaPolicyTests.cs::DefaultWindows_InWords`, `::Window_InWords`, `::Window_WithMinutes_InWords`
- [x] Wrong windows are a problem — test: `SlaPolicyTests::ANonPositiveWindow_IsAProblem`, `::AMoreUrgentPriorityWithMoreTime_IsAProblem`
- [x] Report and priority change store the deadline of the policy — test: `SlaPolicyTests::Report_StoresTheDeadlineOfThePolicy_InTheEvent`, `::ChangePriority_StoresTheNewDeadline_FromTheReportDate`
- [x] The stored deadline wins over a later policy; "late" is judged against it — test: `SlaPolicyTests::TheDeadlineInTheEvents_WinsOverAnotherPolicyLater`, `::Resolve_IsLate_AgainstTheStoredDeadline`
- [x] Old events without a deadline use the default windows — test: `SlaPolicyTests::EventsStoredBeforeTheDeadlineWasRecorded_GetTheDefaultWindows`; `tests/Halcyon.IntegrationTests/SlaAndNumberTests.cs::EventsStoredWithoutADeadline_AreReadWithTheDefaultWindows`
- [x] Configured windows on the real store — test: `SlaAndNumberTests::ConfiguredWindows_GiveTheDeadline_OfAReportAndOfAPriorityChange`
- [x] A restart with other windows does not move existing tickets — test: `SlaAndNumberTests::ARestartWithOtherWindows_DoesNotMoveTheTicketsAlreadyThere`
- [x] Wrong windows stop the host — test: `SlaAndNumberTests::WindowsOutOfOrder_StopTheHost_NamingTheReason`, `::AZeroWindow_StopsTheHost`
- [x] A met number is made again; a number that keeps meeting is refused after 3 attempts; nothing is left behind — test: `SlaAndNumberTests::AReportThatMeetsAGivenNumber_IsMadeAgainUnderANewId`, `::ANumberThatKeepsMeetingGivenOnes_IsRefused_AfterThreeAttempts`
- [x] The seed passes the unique index — test: `SlaAndNumberTests::TheSeed_GivesEveryTicketItsOwnNumber`
- [x] Browser (app with Low = 5 days): agent and public forms name the windows — test: `tests/Halcyon.E2E/SlaTests.cs::AgentForm_ShowsTheConfiguredWindows`, `::PublicForm_ShowsTheConfiguredWindows`
- [x] Browser: lowering the priority previews and stores the configured deadline — test: `SlaTests::LoweringThePriority_PreviewsAndStoresTheConfiguredDeadline_FromTheReportDate`
- [x] Browser: the charts name the configured window — test: `SlaTests::ChartsPage_NamesTheConfiguredWindow`

## Decisions
- [ADR-0013](../../adr/0013-deadline-is-a-fact-sla-from-configuration.md): deadline in the events, windows from the configuration.
- [ADR-0014](../../adr/0014-unique-ticket-number.md): unique number by a unique index, the report made again.
- Kept from Halcyon, not taken from Ambit: decisions in the aggregate (Ambit checks rules in the handler), optimistic
  concurrency with the "someone was faster" message (Ambit locks the stream), one process (Ambit splits files,
  reports and mail into services) - see the comparison in the PR.

## Notes
- The whole E2E run uses Low = 5 days, so every page that names an SLA is checked against the configuration; the
  seed, made by the migrator, keeps the default windows in its events.
