# Requirements and acceptance criteria

Status legend: ✅ implemented and covered by an automated test · ◐ implemented, covered manually or partially · ☐ pending.

## Catalog and detail
- ✅ Cards show title, level, short description, verified language, question count, exam time and availability (E2E `catalog`).
- ✅ Quick look on hover (fine pointers), on keyboard focus, and by a "Quick look" button on touch devices (E2E desktop + mobile).
- ✅ Detail page: syllabus with domain weights and objectives, modes, profile version, verification status, simulator vs official scoring, sources.
- ✅ No fictitious metrics, reviews or prices; empty states explain why (e.g. "In review, none published yet").

## Accounts
- ✅ One account by email for all exams; registration happens on first verified link.
- ✅ Magic link: single use (concurrent verification test), 15-minute expiry, hashed at rest, identical response for any address, IP rate limit, per-address throttle.
- ✅ Roles Student/Admin; admin bootstrap by configured addresses; admin API returns 403 to students.
- ✅ Account deletion removes attempts and the user.
- ✅ CSRF header required on unsafe requests; auth cookie HttpOnly, SameSite=Lax, Secure outside local dev.

## Attempts
- ✅ Practice: optional immediate feedback; single answer can check on select (opt-in) or with "Check answer"; multiple response requires the full selection and an explicit check.
- ✅ Correct and incorrect feedback show the key, the explanation, a rationale for every option and documentation links.
- ✅ Simulation: profile count/time/weights; no solution data in HTML, state or API while active; enabling feedback marks the attempt assisted irreversibly; hiding it again does not erase the exposure.
- ✅ First answer is frozen when the solution is shown; later changes are learning answers, never scored.
- ✅ Immutable snapshot of profile version, question versions, scenarios, option order and item order.
- ✅ Autosave; reload does not reset the timer; the server rejects late answers; sweep expires abandoned attempts.
- ✅ Finish/expire idempotent under concurrent double submit; concurrent answers from two tabs stay consistent.
- ✅ Network loss: the choice is kept and retried; banner explains; the clock keeps running in timed attempts (E2E offline).
- ✅ Not enough eligible questions → clear error, no duplication.
- ✅ Map of answered/pending/marked, previous/next, mark for review, scenario shown above dependent questions, shared-scenario items kept contiguous.
- ☐ CCAR-F official structure "4 of 6 scenarios" in simulations (needs a larger bank: about 15 items per scenario per form).

## Results and progress
- ✅ Points earned/max, percent, correct, incorrect, unanswered, time used, pass/fail against the simulator threshold, scoring policy and disclaimer.
- ✅ By-domain table with numerator/denominator; review with filter; topics to reinforce.
- ✅ Progress separates clean and assisted attempts, excludes admin previews, warns on small samples.

## Administration and content
- ✅ Lifecycle draft → technical_review → editorial_review → approved → published; quarantined/retired with mandatory note.
- ✅ Edits create new versions; attempts keep their version; publishing a version retires older ones.
- ✅ Publication gates: validation, reachable sources, verified language.
- ✅ Review records bound to content hashes; bootstrap applies them idempotently.
- ✅ JSON and Markdown import with preview, all-or-nothing commit, retry without duplicates, exact and near-duplicate detection, inconsistent-key and key-change rejection, size/type/markup limits.
- ✅ Profiles versioned; verified languages require official evidence; coverage and answer-key cue statistics; audit log.
- ☐ PDF/DOCX import (designed extension point only).
- ◐ Admin UI flows are covered by E2E for lists, coverage and import preview; transitions and edits are covered by API integration tests.

## Quality
- ✅ Backend unit + integration tests with real PostgreSQL; frontend unit test; Playwright E2E including axe checks (no serious/critical violations on public pages).
- ✅ Dependency audit (npm audit, dotnet vulnerable packages) and secret scan (gitleaks) run locally; CI workflow defined.
- ◐ CI has not run yet because the repository is not linked to this session.
