# ADR-0008: Attachment bytes live in a private S3 bucket (RustFS locally); the ticket's stream records them

- **Status:** Accepted
- **Date:** 2026-11-03
- **Evidence:** `src/Halcyon.Domain/Tickets/Attachments.cs`, `src/Halcyon.App/Storage/`,
  `src/Halcyon.App/Features/Tickets/Attachments.cs`, `deploy/rustfs/create-bucket.sh`, `docker-compose.yml`
- **Specs:** 002-reporting (files on the report), 005 (ticket page)

## Context
Requesters and agents need to attach files: a screenshot of an application error, marketing material, a contract.
Files are up to tens of MB; PostgreSQL (events and documents, ADR-0002/0003) is the wrong place for the bytes.
The public form is anonymous, so whatever is uploaded must never run in an agent's browser.

## Decision
- The bytes go to an S3-compatible bucket (`Storage:*`; RustFS in compose, `rustfs/rustfs:1.0.0`) under
  `tickets/<ticket:N>/<attachment:N>`. The bucket is private and created by the one-shot `rustfs-init` container
  (idempotent, retries); the app waits for it (`service_completed_successfully`).
- The record is an event, `TicketAttachmentAdded`, in the ticket's stream; `TicketView.Attachments` is its read model.
  The aggregate decides (`Ticket.AddAttachment`): type by extension from a fixed list (no SVG/HTML), at most 20 MB,
  at most 10 per ticket, any state.
- Order of writing: files to S3 first, then the events in one save; a failed save deletes the files it put. A crash
  between the two leaves an orphan object, never an event pointing at nothing.
- Download only for signed-in agents through `GET /zalaczniki/{ticket}/{attachment}`: images and PDF inline, the rest
  as attachment, always `X-Content-Type-Options: nosniff` and `Content-Security-Policy: sandbox`.
- The AWS SDK talks to RustFS with path-style URLs and `RequestChecksumCalculation`/`ResponseChecksumValidation` =
  `WHEN_REQUIRED` (the SDK's default CRC32 trailers are not understood by every S3-compatible server).
- `/health/ready` includes the bucket.

## Alternatives considered
- **Bytes in PostgreSQL (`bytea` / large objects):** simple, one backup - but it bloats the event store, the backup
  and every connection; the demo is meant to show the production shape.
- **Presigned upload straight from the browser:** no bytes through the circuit, but CORS on the bucket, and the
  domain would see a file only after it is already stored; at 20 MB the circuit is good enough.
- **Local disk volume:** no extra container, but does not survive more than one app replica.

## Consequences
- One more stateful service (`rustfs-data` volume) next to PostgreSQL; a backup has to take both.
- Blazor's picker forgets files when the user picks again, so the form reads each file at once: up to 10 × 20 MB in
  the circuit's memory per open form.
- Integration tests use an in-memory storage by default; `RustFsStorageTests` and the e2e run use a real RustFS
  (Testcontainers) so the SDK settings are checked.

## Revisit when
- Files grow past ~50 MB or uploads become frequent - presigned uploads.
- A retention rule is needed (delete files of tickets closed long ago) - a lifecycle rule on the bucket.
