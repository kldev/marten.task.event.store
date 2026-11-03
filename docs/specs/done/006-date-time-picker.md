# 006 · Custom date and time picker

**Status:** done · **Built:** 2026-11-03 → 2026-11-03

## Why
The browser's date input looks different everywhere and cannot express "not in the future" with time.

## What it promises
- A Razor component (`DateTimePicker`) with a month calendar (weeks from Monday), month navigation, hour and minute
  selection, a "Teraz" button, Polish format `dd.MM.yyyy HH:mm`.
- `Max` disables later days; `Min` disables earlier days.
- Keyboard: arrows move the day, Enter picks, Escape closes; ARIA roles on the grid and the dialog.
- Works in `EditForm` (`Value`/`ValueChanged`/`ValueExpression`) and shows validation messages.
- Default value: now.

## Acceptance criteria
- [x] Calendar math (first day, weeks from Monday, disabled range) — `tests/Halcyon.Domain.Tests/CalendarMonthTests.cs`
- [x] Browser: pick a day and time → field shows `dd.MM.yyyy HH:mm` — e2e `ReportTicketTests`
- [x] Browser: a future day is disabled — e2e `ReportTicketTests`
- [x] Browser: keyboard navigation and Escape — e2e `ReportTicketTests`

## Notes
- Calendar math lives in `Halcyon.Domain/Calendar/CalendarMonth.cs` so it is unit-tested without a browser.
