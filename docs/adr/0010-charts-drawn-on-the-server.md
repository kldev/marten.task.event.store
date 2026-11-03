# ADR-0010: Charts are drawn on the server as SVG and HTML, from one query on the projection

- **Status:** Accepted
- **Date:** 2026-11-03
- **Evidence:** the commit that adds spec 009
- **Specs:** 009

## Context
The team lead wants to see how the desk works at a glance: flow in and out, the backlog, deadlines kept, SLA use per
priority, what is burning now, when requests come in. The app is Blazor Server with no JavaScript of its own, and the
image loads nothing from a CDN.

## Decision
- The charts are Razor components that write SVG (lines, columns) or HTML (horizontal bars, the heatmap) on the server.
  Hover tooltips are CSS only (`.chart-hover`, `group-hover`); every chart also has the same numbers as a table.
- `ChartQueries` loads, in one query, every `TicketView` that was open at some moment of the range or was reported in
  it (`ReportedAt < until AND (ResolvedAt IS NULL OR ResolvedAt >= from)`); `ChartQueries.Build` counts everything in
  memory and is tested without a database.
- Buckets are Warsaw calendar days or Monday-Sunday weeks (`ChartBuckets`), like the report periods (ADR-0009).
- The colors: series from the brand ramp (teal, rufous) checked for colour-blind separation; on time / due soon /
  overdue keep the lists' colors and always have a word.

## Alternatives considered
- **A JavaScript chart library (Chart.js, ApexCharts) through JS interop.** Richer interaction, but a second rendering
  path, a CDN or a bundled script, interop calls on every filter change and nothing to assert in a server-side test.
- **A projection per day and category.** The right answer for millions of tickets; for a team desk it is a schema and
  a rebuild path to maintain for numbers a single scan gives.

## Consequences
- No new package, no script, the page works with the circuit alone; charts render in Playwright like any page.
- SVG text scales with the drawing: a chart is drawn for about the width it gets (`W`), the full-width one twice (wide
  and narrow) so the text keeps its size.
- The query scans `TicketView` without an index on `ReportedAt` / `ResolvedAt`: fine for thousands of tickets.

## Revisit when
1. The page gets slow (then duplicated, indexed `ReportedAt` / `ResolvedAt`, or a daily projection).
2. People want to zoom, brush a range or export a chart as an image.
