# Practice Lab — Claude Foundations certification practice

Independent study and simulation platform for two certifications:

- **CCDV-F** Claude Certified Developer – Foundations
- **CCAR-F** Claude Certified Architect – Foundations

It is not official, does not grant any Anthropic certification and does not predict a Pearson VUE result.
Questions are original, mapped to the official exam guides v1.0 (effective July 2026), reviewed independently and
stored server-side. No LLM is called while a student takes an exam.

## What it does

- Catalog with exam cards (hover, keyboard focus or tap for a quick look), detail page with syllabus, weights and modes.
- Email sign-in with single-use magic links. One account for every exam. Roles Student and Admin.
- Practice with optional immediate feedback: why the right option fits and why each other option does not, with documentation links.
- Timed simulation that reproduces the profile (53 items / 120 min for CCDV-F, 60 / 120 for CCAR-F) with solutions hidden.
  Turning feedback on during a simulation marks the attempt as *assisted* for good.
- Custom sets (domains, number of questions, optional time limit).
- Results by domain with numerator and denominator, history, progress that keeps clean and assisted attempts apart.
- Administration: question lifecycle (draft → technical review → editorial review → approved → published, quarantined, retired),
  versioned edits, coverage and answer-key cue statistics, sources, profile versions, verified languages, JSON/Markdown import with preview, audit log.

## Stack

Angular 22 · ASP.NET Core 10 (LTS) · EF Core 10 + Npgsql · PostgreSQL 16 · xUnit v3 · Playwright · Docker Compose.
Modular monolith: one API process serving the built SPA from the same origin.

## Run locally

Requirements: .NET SDK 10.0.401, Node 24 LTS (or 22.22.3+), PostgreSQL 16.

```bash
# database (once)
createuser -P examdev            # password: examdev_local_only (development only)
createdb -O examdev examdev

# API: migrates, bootstraps catalog/sources/banks/reviews, local mailbox
cd backend/src/ExamPrep.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://localhost:5080

# Web (proxy /api → :5080)
cd frontend && npm ci && npx ng serve --port 4200
```

Open http://localhost:4200, sign in with any address; in Development the link is stored in the local mailbox
(`GET /api/dev/outbox?to=you@example.test`). `admin@example.test` is the development administrator.

Docker alternative (builds one image, uses a local Mailpit inbox on http://localhost:8025):

```bash
cp .env.example .env   # set POSTGRES_PASSWORD
docker compose up --build
```

## Tests

```bash
cd backend && dotnet test                                   # unit + integration (needs PostgreSQL; TEST_PG_ADMIN overrides the connection)
cd frontend && npx ng test --watch=false && npx ng build
cd frontend && npx playwright test                           # needs API on :5080 and ng serve on :4200
```

Latest results and coverage: [docs/qa/report.md](docs/qa/report.md).

## Documentation

- [docs/progress.md](docs/progress.md) — status and verifiable pending items
- [docs/environment.md](docs/environment.md) — approved environment scope and what still needs the owner
- [docs/decisions.md](docs/decisions.md) — architecture and product decisions
- [docs/requirements.md](docs/requirements.md), [docs/data-model.md](docs/data-model.md), [docs/api.md](docs/api.md)
- [docs/content-format.md](docs/content-format.md) — JSON and Markdown bank formats, review records
- [docs/sources.md](docs/sources.md) — source hierarchy, exam profile evidence and conflicts
- [docs/bank-coverage.md](docs/bank-coverage.md) — real question inventory per exam and domain
- [docs/operations.md](docs/operations.md) — configuration, deployment and maintenance
- `.claude/agents/` — coordinator, researcher, analyst, programmer and quality agents
