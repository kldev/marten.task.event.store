# ADR-0009: Report fields are code in the category catalog; reports are read from the projection and exported as files

- **Status:** Accepted
- **Date:** 2026-11-03
- **Evidence:** the commit that adds spec 008
- **Specs:** 008

## Context
HR, finance and legalisation need data that a free-text description does not give a spreadsheet: a Limosa number, the
dates of a posting, a new employee's department, an amount and its currency. The data differs per subcategory, part of
it is known only after the ticket was reported, and the owners want reports they can open in Excel or print.

## Decision
- A subcategory lists its `FieldDefinition`s in `CategoryCatalog` (code, like the categories - ADR-0003). A field key is
  stored on tickets: it is never renamed or reused, and one key means one thing in every subcategory
  (`KnownFields` holds them, a test checks the uniqueness).
- Kinds: text, date (`yyyy-MM-dd`, a calendar day), e-mail, choice (with favourites shown first), amount
  (`1234.56`, invariant, two decimals) and IBAN (mod 97). `ReportFields.Check` is the only place the rules live; the
  form only offers the right control.
- The values given at the report travel in `TicketReported.Fields` (null when none - also in every event stored before
  this). Later changes are `TicketFieldsUpdated` with only the changed fields and the label at that moment; allowed in
  any state, like a comment, because numbers often come after the work is done.
- Reports read `TicketView.Fields` (inline projection) for the category reports and the raw `TicketTaken` /
  `TicketResolved` events for the agents' report. Periods are whole calendar units in Warsaw time.
- Exports are built from the one `ReportTable` the page shows: XLSX with ClosedXML (MIT), PDF with QuestPDF (Community
  licence, the Lato font it ships covers Polish), CSV for a Polish Excel (`;`, BOM, decimal comma, formula guard).

## Alternatives considered
- **Fields in the database, edited in the UI.** Flexible, but a renamed or deleted field silently breaks stored tickets and
  the reports; the catalog is already code, and a change is a reviewed commit.
- **One event "fields set" with the full dictionary.** Simpler to fold, but the timeline could not say what changed.
- **CSV only.** No library, but Excel guesses types and encodings, and nobody prints a CSV.
- **A PDF from HTML (headless Chromium).** Same look as the page, but a browser inside the app image.

## Consequences
- Adding a field is a code change and a release; retiring one means leaving it in the catalog (stored values keep
  their label) - the same rule as for subcategories.
- QuestPDF's Community licence is free only for organisations below USD 1M annual revenue; above that a paid licence
  is needed (or another PDF library). `ReportExport` sets the licence type in one place.
- The agents' report scans events of three months in memory: fine for a team desk, not for millions of events.

## Revisit when
1. A field must be added without a release (then a field registry with keys that never die, plus a migration plan).
2. The company outgrows the QuestPDF Community licence.
3. The agents' report gets slow (then a projection per agent and day).
