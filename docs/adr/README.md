# Architecture decision records

| # | Decision |
|---|---|
| [0001](0001-one-blazor-host-and-a-migrator.md) | One Blazor Server host with the logic inside it, and a separate schema migrator |
| [0002](0002-marten-production-setup.md) | Marten in production mode - schema only through the migrator, one pool, inline projections |
| [0003](0003-ticket-as-event-stream.md) | A ticket is an event stream; agents and requesters are documents; categories are code |
| [0004](0004-deadlines-and-alerts.md) | Deadlines are calendar time from the report date; alert state is computed when read |
| [0005](0005-simple-sign-in-and-public-form.md) | Shared password sign-in for agents; requesters do not sign in |
| [0006](0006-idempotent-seed.md) | The seed inserts what is missing by a stable key and never overwrites |
| [0007](0007-three-test-levels.md) | Three separate test levels - domain, integration on real PostgreSQL, browser e2e |
| [0008](0008-attachments-in-s3.md) | Attachment bytes in a private S3 bucket (RustFS locally); the ticket's stream records them |
| [0009](0009-report-fields-and-exports.md) | Report fields are code in the category catalog; reports read the projection and export XLSX / PDF / CSV |
| [0010](0010-charts-drawn-on-the-server.md) | Charts are drawn on the server as SVG and HTML from one query on the projection; no chart library |

New ADR: copy `TEMPLATE.md`, next number, add a line here.
