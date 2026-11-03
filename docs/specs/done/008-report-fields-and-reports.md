# 008 · Report fields, reports and exports

**Status:** done · **Built:** 2026-11-03 → 2026-11-03

## Why
HR, finance and the people legalising foreign workers keep numbers that belong to a ticket - a Limosa number, an A1
form, a meldunek, a new employee's PESEL, an invoice amount - and copy them by hand into spreadsheets. They want the
data collected with the ticket and a report they can export to Excel or PDF. The team lead wants to see how the agents
work in calendar periods.

## What it promises
- A subcategory can collect extra fields; the form shows them after the subcategory is chosen, the ticket page shows
  them and lets any agent fill them in later, in any state. The timeline says what changed.
- Kadry Polska has a new subcategory **Nowy pracownik**: first name, last name (required), e-mail, department (required,
  from a list), country of origin (Europe only, with Polska, Czechy, Niemcy, Belgia, Ukraina, Rumunia first), identity
  document (PESEL / dowód osobisty / brak dokumentów) with its number, start date. A PESEL must have a valid check digit;
  "brak dokumentów" has no number.
- A new category **Zgłoszenie do Finansów** (faktury kosztowe, faktury sprzedażowe i korekty, delegacje i wydatki,
  zaliczka, przelewy i płatności, budżet i MPK) with an amount, a currency and the fields of each topic (invoice number,
  contractor, due date, cost centre, IBAN).
- A new category **Zgłoszenie do rekrutacji**: recruitment for the company and for the agency's clients (client,
  position, how many people, country and place of work, dates, contract, hourly rate, accommodation), and candidates
  and interviews. Marketing has **Budżet na kampanię** with several channels at once (Facebook, Instagram, TikTok,
  LinkedIn, job boards...), the goal, the amount and a linked ticket. A recruitment ticket orders a campaign from
  marketing with one button: the new ticket is prefilled (topic, goal "Rekrutacja", position, headcount, the
  recruitment ticket's number, the requester) and listed on the recruitment ticket afterwards.
- The seed has 10 recruiters (department Rekrutacja; 40 people in all) and 200 generated tickets for the lists.
- **Osoba realizująca**: nobody writes to HR directly; the agent who took a ticket can name a person from the category's
  department (Kadry for HR and legalisation, Rekrutacja, Finanse; anybody elsewhere) to do the work, with a reason -
  e.g. the system does not let the agent do it. The ticket stays with the agent; the person is on the ticket, the list,
  the timeline and the reports.
- The lists are paged (25 per page, in the database, open first by deadline then resolved latest first); the total is
  counted only with **Policz wszystkie**; a new filter goes back to page 1. A search box finds a ticket by a part of
  its title or by its HAL number.
- **Raporty**: Kadry Polska, Kadry Zagranica, Finanse, Rekrutacja, Marketing - the tickets of the category by report date, the fields as columns
  (only columns that have data), filters: report date from-to (Warsaw days, both ends included), market, subcategory,
  all / only open / only resolved.
- **Praca agentów**: per agent, tickets taken, resolved and resolved late in the current calendar week (Monday to
  Sunday - on a Friday it is still that whole week), the current month and the current plus two previous months; or the
  previous complete periods. Plus what each agent has in progress now, and a totals row.
- Every report exports to XLSX (real dates and numbers, a table with filters), PDF (landscape, header on every page) and
  CSV (for a Polish Excel), for signed-in agents only.
- The list of all tickets has the same "all / only open / only resolved" toggle.
- The version and the git commit are shown in the sidebar and on the public pages, and logged at start.

## Surface
- Domain: `FieldDefinition`, `ReportFields`, `KnownFields` in the catalog, `TicketReported.Fields`, `TicketFieldsUpdated`,
  `Ticket.UpdateFields`, `ReportPeriods`.
- Database: `TicketView.Fields`, projection, event type, seed (`FillFields` step, HR and finance tickets with data).
- App: `Features/Reports/` (queries, exports, endpoints), `UpdateTicketFields`, `CompletionFilter`, pages under
  `/raporty`, `DatePicker`, `ReportFieldInputs`, `ReportTableView`, `CompletionToggle`, `AppVersion`.

## Out of scope
- Fields defined in the UI (not ever, ADR-0009), required fields that differ between the public form and agents (not yet).
- Saved report filters and scheduled e-mailing of reports (not yet).
- Fields for Legalizacja (not yet - the category has none so far).

## Acceptance criteria
- [x] Values trimmed, empty not stored, unknown key / too long / bad date / bad e-mail / bad choice refused — `tests/Halcyon.Domain.Tests/ReportFieldsTests.cs`
- [x] New employee: owner's fields, required name and department, Europe with favourites first — `ReportFieldsTests`
- [x] PESEL check digit; document and number go together — `ReportFieldsTests`
- [x] Amount (two decimals, Polish and invariant input) and IBAN (mod 97) — `ReportFieldsTests::FinanceFieldsTests`
- [x] One key means one thing in the catalog; every choice has options — `ReportFieldsTests`
- [x] Fields stored at the report; update records only the changes, no-op adds nothing, allowed after resolving — `ReportFieldsTests::TicketFieldsTests`, `tests/Halcyon.IntegrationTests/ReportTests.cs`
- [x] An event stored before fields existed still reads — `ReportTests::AnEventStoredBeforeFieldsExisted_StillReads`
- [x] Report: category only, report order, only field columns with data, market column only across markets — `ReportTests`
- [x] Filters combine; Warsaw days with both ends included — `ReportTests`
- [x] CSV: BOM, `;`, decimal comma, formula guard; XLSX: dates and amounts as values, text never a formula; PDF renders — `ReportTests`
- [x] Exports need sign-in; unknown report or format is 404 — `ReportTests`
- [x] Calendar periods (Monday-Sunday, whole months, across the new year, Warsaw midnight) — `tests/Halcyon.Domain.Tests/ReportPeriodsTests.cs`
- [x] Agents' report counts taken / resolved / late per period, handed-over tickets count for who resolved them — `ReportTests::AgentsReport_CountsTakenResolvedAndLate_InCalendarPeriods`
- [x] All tickets: only open / only resolved — `ReportTests::AllTickets_CanShowOnlyOpen_OrOnlyResolved`
- [x] Recruitment (company / client), headcount, several channels, linked ticket number — `ReportFieldsTests::RecruitmentFieldsTests`
- [x] Recruitment orders a prefilled campaign and sees it linked — `ReportTests::Recruitment_OrdersACampaign_AndSeesItLinked`, e2e `RecruitmentCampaignTests`
- [x] Handler: only the agent the ticket is with, only from the department, same person is no change, null takes off — `tests/Halcyon.Domain.Tests/HandlerTests.cs`; on the view, timeline and report — `ReportTests::TheAgent_NamesAHandlerFromKadry_ShownOnTheListTimelineAndReport`; browser — e2e `PagingAndHandlerTests`
- [x] Pages together are the whole list with no gaps or duplicates, also across open → resolved; total on request — `tests/Halcyon.IntegrationTests/PagingTests.cs`; browser — e2e `PagingAndHandlerTests`
- [x] Search by title part or number, % and _ are letters — `PagingTests::Search_FindsByAPartOfTheTitle_OrTheNumber_AndWildcardsAreLetters`
- [x] Ticket numbers of tickets reported the same minute differ (defect fixed, see Notes) — `ReportingTests::Number_OfTicketsReportedTheSameMinute_Differs`
- [x] Browser: new employee with a wrong PESEL refused, completed on the ticket page, in the report, toggle, XLSX and PDF downloaded, version shown — `tests/Halcyon.E2E/ReportFieldsTests.cs`

## Decisions
- ADR-0009.
- The country list is Europe only (no Turkey, Caucasus or Kazakhstan); favourites are the owner's six.
- "Dowód osobisty" has no format check: a foreign ID card has its own; only spaces are removed and letters capitalised.
- Agents' report: "taken" counts `TicketTaken` only (a handover is not a take); "resolved" is whoever resolved it.
- The report page keeps its filters in the component, the export links carry them in the query (`od`, `do`, `rynek`,
  `podkategoria`, `stan`); an unreadable value is ignored, not refused.

## Notes
- **Defect found and fixed:** the HAL number was the first 8 hex digits of the ticket id, and a reported ticket's id is a
  version 7 GUID whose first digits are a timestamp - every ticket reported within ~65 s got the same number (seen
  when linking a campaign to a recruitment ticket by number). Version 7 ids now use their last 8 (random) digits.
  Numbers already stored in `TicketView` stay as they were; older duplicates there are not renumbered.
- The SDK puts the commit into the informational version from `.git`; the image has no `.git`, so `up.sh` passes
  `GIT_SHA` (with `-dirty` for uncommitted changes) as a build argument.
