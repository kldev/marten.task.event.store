# ADR-0005: Agents sign in with their e-mail and one shared password from the environment; requesters do not sign in

- **Status:** Accepted
- **Date:** 2026-11-03
- **Evidence:** `src/Halcyon.App/Auth/*`, `Components/Pages/Public/Report.razor`
- **Specs:** 001-agents-and-sign-in, 002-reporting

## Context
The product owner asked for a very simple sign-in ("not production"), a password from `.env`, and requesters who are
not agents and never sign in - they pick themselves from a list.

## Decision
- **Agents:** e-mail (case-insensitive, must exist) + one shared password from `Auth:AgentPassword`
  (`Auth__AgentPassword`, in compose `AGENT_PASSWORD` from `.env`). Missing password → the host refuses to start.
  The password is compared in constant time; a wrong e-mail and a wrong password give the same message; the log has
  neither the address nor the password. A cookie session (sliding 8 h). Sign-in is rate limited.
- The sign-in page is static server rendering with a plain form posting to `/auth/login` (an interactive circuit
  cannot write a cookie); everything else is interactive and requires a signed-in agent - through `[Authorize]` on
  every page (`Components/Pages/_Imports.razor`), **not** a fallback policy, which would also close Blazor's own
  `/_blazor` endpoints to the anonymous public form. Too many attempts (`Auth:SignInAttemptsPerMinute`, 10) send the
  visitor back to the form with a message.
- **Requesters (people)** never sign in. The public page `/zglos` lets a person pick themselves from the list (or
  "incognito") and report. **Picking a name is not authentication:** anyone can pick anyone. Accepted on purpose for an
  internal tool; the page only creates tickets and reveals nothing but names on the list. It is rate limited and uses
  the Blazor antiforgery protection.
- Agents and people are separate lists; an agent's e-mail cannot be used as a person.

## Alternatives considered
- **Per-agent passwords / ASP.NET Identity:** more moving parts than asked for.
- **SSO:** not available for this tool.

## Consequences
- Anyone who knows the shared password and an agent's e-mail is that agent. Fine for a demo, not for production.

## Revisit when
- The app leaves the internal network or holds sensitive data.
