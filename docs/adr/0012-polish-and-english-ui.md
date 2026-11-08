# ADR-0012: The UI speaks Polish and English; the domain and the data stay Polish

- **Status:** Accepted
- **Date:** 2026-11-08
- **Evidence:** branch `feat/i18n-en`
- **Specs:** [011](../specs/done/011-polish-and-english.md)

## Context
Halcyon was written for a Polish team: every screen, the category catalog, the domain's refusals and the seed are in
Polish. The project is also shown to people who do not read Polish (a video, the repository's readers), so the screens
need English - without touching the stored events, whose keys and values the catalog defines.

## Decision
- Two cultures, `pl-PL` (the default) and `en-GB`, chosen only by the culture cookie (`CookieRequestCultureProvider`), never
  by the browser's Accept-Language: the team works in Polish whatever their browsers say.
- `GET /jezyk/{pl|en}?returnUrl=…` writes the cookie and comes back (local paths only). The switch (`Ui/LanguageSwitch`)
  is a plain link in the top bar and on the sign-in and public pages: a Blazor circuit keeps the culture of the request
  that opened it, so changing the language is a full load.
- UI texts are written in both languages where they are used: `T("Pula", "Pool")` (`Localization/Tr.cs`). No resource
  files and no keys: a page reads like before, with its English next to the Polish.
- The domain keeps its Polish names and messages. The app translates them for display: `N(name)` for the catalog
  (categories, topics, report fields, choices, priorities, statuses, months, departments) and `Err(message)` for a
  `DomainException`, from one dictionary (`Localization/CatalogText.cs`) and patterns for messages with a name or a number.
  Stored keys and choice values never change; a text the dictionary does not know shows in Polish.
- What people typed (titles, descriptions, comments, names) and the seed's data are shown as they are.
- Texts made without a reader (background jobs' descriptions and results) are functions evaluated when a page asks.

## Alternatives considered
- **resx + `IStringLocalizer`**: the standard, but several hundred keys for a two-language internal tool, and every page
  becomes a list of keys to look up. Inline pairs keep the Polish wording, which tests and people know, in place.
- **English in the domain** (codes for refusals, names per language in the catalog): cleaner in theory, but it changes
  the domain's API and its tests for a display concern; the catalog is code and its Polish names are its identity.
- **Accept-Language**: would switch a Polish team to English on an English browser.

## Consequences
- A new UI text needs both languages - `T("…")` with one argument does not compile.
- A new catalog name or domain message needs a line in `CatalogText`, or it shows in Polish in the English UI.
- Exports (XLSX, PDF, CSV) follow the language of the request that downloads them.

## Revisit when
1. A third language - then resource files per language beat pairs.
2. The domain gets non-Polish users of its own (an API) - then refusals need codes.
