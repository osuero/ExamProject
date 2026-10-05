# Progress

Last updated 2026-10-05. This file is the hand-off point: it states what exists and what is verifiably pending. Nothing continues running after a session ends.

## Done (verified)
- Environment: .NET 10.0.401, Node 24.21.0, PostgreSQL 16 in the cloud workspace; scope recorded in `docs/environment.md`.
- Research: official exam guides v1.0 for both exams, Partner Academy FAQ, Pearson pages; conflicts recorded (`docs/sources.md`, `docs/research/profile_research.json`).
- Backend: catalog, magic-link auth, attempts engine (practice, simulation, custom), feedback rules, results, progress, admin, workflow, review records, JSON/Markdown import, audit, expiry sweep.
- Frontend: home, catalog with quick look, exam detail with start options, sign-in, runner, result, my exams, progress, admin (questions, coverage/quality, import, sources, profile and languages, audit), question detail with transitions and versioned edits.
- Content: 331 questions published (169 CCDV-F, 162 CCAR-F) after blind independent review in two batches; review records hash-bound.
- CCAR-F simulations draw from 4 of the 6 scenarios (profile v2).
- Import: JSON, Markdown, Word (.docx) and PDF with location provenance.
- GitHub: `main` pushed by the owner on 2026-10-05; first CI run passed (backend, frontend, e2e, secrets).
- Tests: backend 58/58, frontend unit 3/3, E2E 14/14 including axe on signed-in pages (see `docs/qa/report.md`). Audits: no vulnerable packages, no npm advisories, no secrets.
- Agents: `.claude/agents/` coordinator, researcher, analyst, programmer, quality.
- Delivery files: Dockerfile, docker-compose.yml, .env.example, CI workflow, documentation.

## Blocked on the owner
- [ ] Link GitHub (`osuero`) in claude.ai → Settings → Connectors so the session can push and open PRs itself. Until then the owner pushes from the local clone.
- [ ] Decide whether the banks with answer keys should stay in a public repository.
- [ ] Choose hosting and production database; provide secrets (DB connection, SMTP credentials). No deployment has been made.
- [ ] Choose a transactional email provider and verify the sender domain.
- [ ] Decide merge/deploy automation and branch protection.

## Next work (independent of the owner)
- [ ] Grow the banks toward 300 per exam in reviewed batches (now 169 and 162). Largest remaining gaps: CCDV-F D2 (54/99), D5 (29/51), D1 (25/44), D6 (18/33), D8 (17/32); CCAR-F A1 (46/81), A2 (25/54), A3 (34/60), A4 (34/60), A5 (23/45). Aim for the correct option being the longest in about 25 % of items.
- [ ] Keep practice items and any reserved assessment forms in separate families once a reserved pool exists.
- [ ] Re-verify version-sensitive items (model behaviour, beta features, hook field names) when the docs change; record the date in `sourceCheckedAt`.
- [ ] Frontend coverage report.
- [ ] Build and run the Docker image (no Docker daemon in the workspace; CI does not build it yet).
- [ ] Re-check items that depend on Claude Haiku 4.5 after its retirement date (not sooner than 2026-10-15).
- [ ] Human subject-matter review of the bank and calibration of difficulty with real attempt data.

## How to resume
1. Read `CLAUDE.md`, this file and `docs/decisions.md`.
2. Start PostgreSQL, run the API and `ng serve` (see README); the bootstrap loads catalog, sources, banks and reviews.
3. Run the three test suites before and after changes.
