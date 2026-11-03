# 001 · Agents and sign-in

**Status:** done · **Built:** 2026-11-03 → 2026-11-03

## Why
Agents need to get in quickly and see who else is on the team, how to reach them and how much each one is carrying.

## What it promises
- An agent signs in with the company e-mail (any letter case) and the shared password from the environment.
- A wrong e-mail and a wrong password give the same message; nothing about either lands in the log.
- Without the password configured the app does not start and says why.
- Every page except sign-in, the public report form and `/health*` requires a signed-in agent.
- A signed-in agent sees all agents: first and last name, phone, company e-mail, tickets in progress.
- A signed-in agent sees all people (requesters), read only.
- Agents have no status (no active/inactive, no availability).

## Surface
- `Halcyon.App/Auth`, `Components/Pages/SignIn.razor`, `Pages/Agents.razor`, `Pages/People.razor`.

## Out of scope
- Per-agent passwords, roles, agent administration (not yet).

## Acceptance criteria
- [x] Correct password signs in, e-mail case does not matter — `tests/Halcyon.IntegrationTests/SignInTests.cs`
- [x] Wrong password / unknown e-mail → same error — `SignInTests`
- [x] Missing `Auth:AgentPassword` → host does not start — `SignInTests`
- [x] Anonymous request to a page redirects to sign-in; `/zglos` and `/health` are open — `SignInTests`
- [x] Agents list shows name, phone, e-mail and in-progress count — `IntegrationTests/AgentsQueryTests.cs`, e2e `AgentsAndPeopleTests`
- [x] Sign-in, sign-out and redirect in the browser — e2e `tests/Halcyon.E2E/SignInTests.cs`
- [x] Too many attempts → back to the form with a message (10 a minute, configurable) — `SignInTests`
- [x] Blazor's own endpoints are open, so the public form connects — `SignInTests`

## Decisions
- ADR-0005.

## Notes
- A fallback "authenticated user" policy also closed `/_blazor` to anonymous visitors: the public form rendered
  empty. Pages are now `[Authorize]` through `Components/Pages/_Imports.razor` (found by e2e, regression test added).
