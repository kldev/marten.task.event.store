# Specs (Spec Driven Development)

One spec per feature, written before the code. A spec moves to `done/` when every acceptance criterion has a passing
test. A changed decision updates the spec and its ADR in the same commit.

| Spec | Feature | Tests |
|---|---|---|
| [001](done/001-agents-and-sign-in.md) | Agents and sign-in | Integration `SignInTests`, `AgentsQueryTests`; e2e `SignInTests`, `AgentsAndPeopleTests` |
| [002](done/002-reporting.md) | Reporting (agent and public form) | Domain `ReportingTests`, `CategoryCatalogTests`; integration `TicketWorkflowTests`; e2e `ReportTicketTests`, `PublicReportTests` |
| [003](done/003-pool-and-work.md) | Pool, take, handover, return, resolve | Domain `WorkflowTests`; integration `ConcurrencyTests`, `TicketQueriesTests`; e2e `WorkTests` |
| [004](done/004-deadlines-and-alerts.md) | Deadlines, warnings, alerts, priority change | Domain `DeadlineTests`, `TimeLeftTextTests`, `PriorityTests`; integration `TicketQueriesTests`; e2e `AlertTests` |
| [005](done/005-comments-and-timeline.md) | Comments and timeline | Domain `CommentTests`; integration `TicketWorkflowTests`; e2e `CommentTests` |
| [006](done/006-date-time-picker.md) | Custom date and time picker | Domain `CalendarMonthTests`; e2e `ReportTicketTests` |
| [007](done/007-database-and-seed.md) | Production Marten setup and seed | Integration `MigratorTests`, `SeedTests` |
| [008](done/008-report-fields-and-reports.md) | Report fields, recruitment and campaigns, handler from the department, paged lists with search, reports with XLSX / PDF / CSV | Domain `ReportFieldsTests`, `ReportPeriodsTests`, `HandlerTests`; integration `ReportTests`, `PagingTests`; e2e `ReportFieldsTests`, `RecruitmentCampaignTests`, `PagingAndHandlerTests` |
| [009](done/009-charts.md) | Charts: flow, backlog, deadlines kept, SLA use, queue now, categories, agents, heatmap | Domain `ChartBucketsTests`; integration `ChartNumbersTests`, `ChartTests`; e2e `ChartsTests` |
| [010](done/010-system-status-logs-database-queues.md) | System menu: status with HTTP statistics (200 / 403 / 500), the log, the database, the queues; 14-day retention | Integration `MonitoringTests`, `MigratorTests`; e2e `SystemPagesTests` |
| [012](done/012-ported-from-ambit.md) | Configurable SLA with the deadline stored in the events; unique ticket number (from Ambit) | Domain `SlaPolicyTests`; integration `SlaAndNumberTests`; e2e `SlaTests` |
| [013](done/013-email-notifications.md) | E-mail notifications (confirmation, handover, resolved, deadline warning and alert) over Wolverine handlers and Marten's outbox | Integration `NotificationTests`, `MailpitTests`, `MigratorTests`, `ConcurrencyTests`; e2e `NotificationTests` |

## Requirement → spec

| Requirement (owner) | Spec |
|---|---|
| Agent collects a ticket, it goes to the pool, an agent takes it | 002, 003 |
| Categories with subcategories (IT, Legal, Marketing) | 002 |
| Blazor + Tailwind, custom date control | 006, all UI |
| Report date + priority, SLA 3 h / 8 h / 3 days / 1 week | 002, 004 |
| Time left in hours, < 1 h formatted differently, overdue = other alert | 004 |
| Edge cases | every spec |
| Not multi-tenant | ADR-0001 |
| Simple sign-in, password from `.env` | 001 |
| Agents see agents; name, phone, `company.io` e-mail; no status | 001 |
| People (non-agents) without sign-in pick themselves; incognito | 002 |
| Requester sets the priority, agent can change it, deadline from the report date | 002, 004 |
| Handover to another agent, return to the pool | 003 |
| Comments | 005 |
| Idempotent seed (10 agents, 40 people - 30 + 10 recruiters since spec 008), no overwrite | 007 |
| Production Marten setup | 007, ADR-0002 |
| Domain, integration and e2e tests separately | ADR-0007 |
| Extra data per topic (Limosa, meldunek, new employee, amount) and HR / finance reports with Excel and PDF export | 008 |
| How the agents work: week, month, 3 months (calendar) | 008 |
| Version and git commit on the page | 008 |
| Recruitment (company and clients), campaign budgets (Facebook, TikTok...), recruitment orders campaigns | 008 |
| Agent names a person from HR (the department) to do the work | 008 |
| 200 tickets in the seed, paging, "count total" button | 008 |
| Charts besides the reports | 009 |
| SLA windows per deployment; a deadline already given never moves; numbers never repeat | 012 |
| Mail to the requester and the agents; deadline warnings that survive a restart; production SMTP configuration | 013 |
