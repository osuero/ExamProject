# Progress

Last updated 2026-10-03. This file is the hand-off point: it states what exists and what is verifiably pending. Nothing continues running after a session ends.

## Done (verified)
- Environment: .NET 10.0.401, Node 24.21.0, PostgreSQL 16 in the cloud workspace; scope recorded in `docs/environment.md`.
- Research: official exam guides v1.0 for both exams, Partner Academy FAQ, Pearson pages; conflicts recorded (`docs/sources.md`, `docs/research/profile_research.json`).
- Backend: catalog, magic-link auth, attempts engine (practice, simulation, custom), feedback rules, results, progress, admin, workflow, review records, JSON/Markdown import, audit, expiry sweep.
- Frontend: home, catalog with quick look, exam detail with start options, sign-in, runner, result, my exams, progress, admin (questions, coverage/quality, import, sources, profile and languages, audit), question detail with transitions and versioned edits.
- Content: 142 questions published (71 per exam) after blind independent review; review record hash-bound.
- Tests: backend 52/52, frontend unit 3/3, E2E 13/13 (see `docs/qa/report.md`). Audits: no vulnerable packages, no npm advisories, no secrets.
- Agents: `.claude/agents/` coordinator, researcher, analyst, programmer, quality.
- Delivery files: Dockerfile, docker-compose.yml, .env.example, CI workflow, documentation.

## Blocked on the owner
- [ ] Link GitHub (`osuero`) to the session, then push `main` and the feature branch and open the PR. The repository is public; decide whether the banks with keys should be in a public repository.
- [ ] Choose hosting and production database; provide secrets (DB connection, SMTP credentials). No deployment has been made.
- [ ] Choose a transactional email provider and verify the sender domain.
- [ ] Decide merge/deploy automation and branch protection.

## Next work (independent of the owner)
- [ ] Grow the banks toward 300 per exam in reviewed batches (current 71/300 each). Priorities by gap: CCDV-F D2, D5, D1; CCAR-F A1, A3, A4.
- [ ] CCAR-F: sample 4 of the 6 scenarios per simulation as the official guide describes (needs ≈ 15+ items per scenario per domain mix).
- [ ] Keep practice items and any reserved assessment forms in separate families once a reserved pool exists.
- [ ] Re-verify version-sensitive items (model behaviour, beta features, hook field names) when the docs change; record the date in `sourceCheckedAt`.
- [ ] Add axe scans for the runner and admin pages; frontend coverage report.
- [ ] Run the Docker image and the CI workflow once a daemon/runner is available.
- [ ] PDF/DOCX importer (designed, not implemented).
- [ ] Human subject-matter review of the bank and calibration of difficulty with real attempt data.

## How to resume
1. Read `CLAUDE.md`, this file and `docs/decisions.md`.
2. Start PostgreSQL, run the API and `ng serve` (see README); the bootstrap loads catalog, sources, banks and reviews.
3. Run the three test suites before and after changes.
