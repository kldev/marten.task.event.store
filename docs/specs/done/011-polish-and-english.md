# 011 · Polish and English

**Status:** done · **Built:** 2026-11-08 → 2026-11-08

## Why
The app is shown to people who do not read Polish (a video, the repository), and an agent abroad may prefer English.
The team keeps working in Polish by default (ADR-0012).

## What it promises
- A **PL / EN** switch in the top bar of every agent's page, on the sign-in page and on the public form; the choice is
  kept for a year in the culture cookie and comes back to the same page.
- Polish is the default for everybody; a browser's language does not change it.
- In English: every screen, menu, button, hint, empty state, chart, report and export (XLSX / PDF / CSV); the category
  catalog (categories, topics, report fields and their choices), priorities, statuses, months and departments; the
  domain's refusals (e.g. "Someone was faster: …").
- What people typed and the demo data stay as they were written.
- Stored data does not change: the same events and choice values in both languages.

## Surface
- App: `Localization/Language.cs` (cultures, `/jezyk/{code}`), `Localization/Tr.cs` (`T`, `N`, `Err`),
  `Localization/CatalogText.cs` (the dictionary), `Components/Ui/LanguageSwitch.razor`; every page and component.

## Out of scope
- A third language; translated demo data; per-agent language stored in the database (the cookie is per browser).
