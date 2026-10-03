# Project instructions

Independent practice platform for CCDV-F and CCAR-F. Read `docs/progress.md` before starting work.

## Layout
- `backend/src/ExamPrep.Api` — ASP.NET Core 10 modular monolith. `Modules/` (Auth, Catalog, Attempts, Admin, Import, Workflow, Bootstrap), `Domain/ExamRules.cs` (pure scoring, allocation, assembly, lifecycle, validation), `Data/` (EF Core entities, migrations).
- `backend/tests/ExamPrep.Tests` — xUnit v3 unit + integration tests against real PostgreSQL (`dotnet test` from `backend/`).
- `frontend/` — Angular 22 standalone components, signals; `e2e/` Playwright.
- `content/` — backend-only: `catalog.json`, `sources.json`, `banks/*.json|md` (with answer keys), `reviews/*.json` (review records bound to content hashes). Never serve or bundle these in the frontend.
- `docs/` — requirements, decisions, API, data model, QA, progress.

## Invariants (do not break)
- Keys, rationales and explanations are only returned for revealed items or finished attempts.
- The server owns attempt time; answers after the deadline are rejected; finish/expire are idempotent (row lock).
- Every attempt query filters by the authenticated user; admin routes require role Admin.
- Unsafe `/api` requests require `X-XSRF-TOKEN`.
- Question versions are immutable; edits create versions; attempts keep the version they used. Profiles are versioned too.
- Publication requires: no validation errors, registered reachable sources, and a verified exam language with official evidence.
- The 720/1000 official scaled score is never converted to a percentage; the 80% pass mark is a simulator rule.

## Content rules
Original questions only. No dumps, leaked items or copied official sample questions. Verify technical claims in official
docs. Independent blind review before publication; review records apply only to the exact content hash reviewed.

## Commands
- API: `cd backend/src/ExamPrep.Api && ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://localhost:5080`
- Web: `cd frontend && npx ng serve --port 4200`
- New migration: `cd backend/src/ExamPrep.Api && dotnet ef migrations add <Name> -o Data/Migrations`
