# 009 · Charts

**Status:** done · **Built:** 2026-11-03 → 2026-11-03

## Why
The reports (spec 008) are tables to export. The team lead also wants to see at a glance how the desk is doing: is the
pool growing or shrinking, do we keep the deadlines, which teams send the most, how much of the SLA a ticket uses, what
is burning right now and when the requests come in. A table of 30 days answers none of that quickly.

## What it promises
- A page **Wykresy** (`/wykresy`, in the menu under Raporty and as a tile on `/raporty`) for signed-in agents.
- Filters in one row: the range - **7 dni**, **30 dni** (daily) or **12 tygodni** (calendar weeks Monday-Sunday) - and
  the category (all, or one). The range always ends today; days and weeks are Warsaw calendar units, like the reports.
- Key figures: reported and resolved in the range, share resolved on time, median time to resolve, open now with how
  many are due soon and overdue now.
- **Napływ i realizacja**: reported and resolved per day / week (two lines, one axis).
- **Otwarte na koniec dnia**: how many were open at the end of each day / week (today: now) - the backlog trend.
- **Terminowość**: resolved per day / week, split on time / after the deadline.
- **Zużycie SLA wg priorytetu**: for tickets resolved in the range, the median and the 90th percentile of the time
  to resolve as a share of the ticket's SLA, with the 100 % line; the median also in hours / days.
- **Wg kategorii** (or **wg podkategorii** when one category is chosen): tickets reported in the range, split
  resolved on time / resolved late / still open; the biggest first.
- **Kolejka teraz**: open tickets per priority, split on time / due soon / overdue at this moment (the range does not
  apply; the category does).
- **Kiedy przychodzą zgłoszenia**: a weekday × hour heatmap of the reports in the range (Warsaw time).
- **Kto realizuje**: resolved in the range per agent who resolved, on time / late; the top 8, the rest as "Pozostali".
- Every chart: a legend for two or more series, a hover tooltip with the exact values, and **Pokaż tabelę** with the same
  numbers as a table. Status colors (on time / due soon / overdue) are those of the lists, always with a word.
- Rendered on the server as SVG and HTML: no chart library, no JavaScript, no CDN.

## Surface
- Domain: `ChartBuckets` (days and calendar weeks in Warsaw time).
- App: `Features/Reports/ChartQueries.cs` (one query on `TicketView`, the numbers computed in memory),
  `Components/Ui/Charts/*` (line, stacked columns, stacked bars, SLA bars, heatmap, card), page
  `Pages/Reports/ChartsPage.razor`, menu and the tile on `/raporty`.

## Out of scope
- Exporting a chart as an image (not yet - the tables have exports).
- Custom date ranges and drill-down from a bar to the list (not yet).
- Charts per report field (amounts, markets) (not yet).
- A projection per day for big volumes (not yet - ADR-0010, revisit).

## Acceptance criteria
- [x] Days and calendar weeks end today, Warsaw midnights, a DST day is 25 h, weekday and hour in Warsaw time — `tests/Halcyon.Domain.Tests/ChartBucketsTests.cs`
- [x] Reported / resolved / on time / late per bucket, open at the end of each bucket (also a ticket reported before the range; today = now) — `tests/Halcyon.IntegrationTests/ChartTests.cs::ChartNumbersTests.Flow_CountsPerBucket_AndTheBacklogAtTheEndOfEach`
- [x] Key figures: on-time share, median time to resolve, open / due soon / overdue now; none when nothing resolved — `ChartNumbersTests.KeyFigures`, `KeyFigures_WithNothingResolved_HaveNoShareAndNoMedian`
- [x] SLA use per priority: median and P90 (nearest rank, from 5 tickets) as a share of the ticket's own SLA — `ChartNumbersTests.SlaUse_IsAShareOfTheTicketsOwnSla`, `MedianAndNearestRankP90`
- [x] By category, biggest first, split by outcome — `ChartNumbersTests.ByCategory_BiggestFirst_SplitByOutcome`
- [x] Queue now per priority with deadline states — `ChartNumbersTests.QueueNow_ByPriority_WithDeadlineStates`
- [x] Agents top 8, the rest folded — `ChartNumbersTests.Agents_TopEight_TheRestFolded`
- [x] Heatmap by Warsaw weekday and hour — `ChartNumbersTests.Heatmap_UsesWarsawWeekdayAndHour`
- [x] Weeks are whole calendar weeks — `ChartNumbersTests.Weeks_CountWholeCalendarWeeks`
- [x] The query loads tickets open during the range and resolved in it — `ChartTests.TheQuery_LoadsTicketsOpenDuringTheRange_AndResolvedInIt`
- [x] One category: split by subcategory, every chart filtered — `ChartTests.CategoryFilter_SplitsBySubcategory_AndFiltersEveryChart`
- [x] Browser: every chart renders, the range and the category change them, a table view opens — `tests/Halcyon.E2E/ChartsTests.cs::EveryChartRenders_AndTheRangeAndTheCategoryChangeThem`
- [x] Signed-out visit goes to sign-in — `ChartsTests::SignedOut_GoesToSignIn`

## Decisions
- ADR-0010: charts drawn on the server, no chart library.
- The resolver of a ticket is `TicketView.AssigneeId` after `TicketResolved` (a handover moves it, a resolve keeps it),
  the same person the agents' report counts.
- The SLA of a ticket is `Deadline - ReportedAt`: after a priority change the deadline moves from the report date
  (ADR-0004), so the share is against the SLA the ticket really had.
- P90 is the nearest rank; with fewer than 5 resolved tickets of a priority only the median is drawn.
- Colors: reported halcyon-500, resolved rufous-500 (checked for colour-blind separation); on time emerald-600, due soon
  amber-500, overdue / late red-700 - the lists' meaning; open (not yet decided) ink-300.

## Notes
- **Defect found and fixed while building:** the last point of the backlog line counted a ticket resolved at this very
  instant as still open (`ResolvedAt >= now`), so it disagreed with "Otwarte teraz". The bucket holding now counts the
  tickets that are not done.
- The page overflowed a phone (626 px wide): a grid item keeps its content's minimum width, and the heatmap has one.
  `min-w-0` on the card lets the heatmap scroll inside it.
- The seed's history is 28 days, so "12 tygodni" is empty before that, and the seed resolves most tickets late - the
  SLA chart shows it plainly.
