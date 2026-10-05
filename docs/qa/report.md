# QA report

Last run 2026-10-05 in the cloud workspace (Linux x64, .NET SDK 10.0.401, Node 24.21.0, PostgreSQL 16.15, Chromium 1194). Raw outputs are next to this file.
GitHub Actions: the first CI run on `main` (run 37323260956, commit 60ca785) passed all four jobs: backend, frontend, e2e and secrets.

## Automated tests (all executed, real results)
| Suite | Command | Result | Output |
|---|---|---|---|
| Backend unit + integration (real PostgreSQL, real content bootstrap) | `cd backend && dotnet test -- --coverage --coverage-output-format cobertura` | **58 passed, 0 failed**, 0 build warnings | backend-tests-output.txt |
| Frontend unit (Vitest via Angular) | `cd frontend && npx ng test --watch=false` | **3 passed** | frontend-unit-output.txt |
| Frontend production build | `cd frontend && npx ng build` | success, initial bundle 288.8 kB raw / 79.6 kB transfer | frontend-build-output.txt |
| End-to-end (Playwright, desktop + mobile, axe-core) | `cd frontend && npx playwright test` | **14 passed, 0 failed** | e2e-output.txt |

### Added on 2026-10-05
- CCAR-F simulations use exactly 4 of the 6 scenarios (integration test, three runs; unit tests for subset selection and open fallback).
- Word (.docx) and PDF import: parsing with paragraph/page locations, DTD rejection, corrupt files reported as errors (unit tests); PDF upload through the API (integration test).
- Accessibility: axe-core now also scans the exam runner (before and after feedback), result, my exams, progress and admin pages: no serious or critical violations.
- Bootstrap test accepts every review record (QA-B1, QA-B2).

### What the suites cover
- Auth: identical request-link responses, concurrent single-use tokens, expiry, hashed storage, CSRF required, admin/student/anonymous access.
- Attempts: simulation profile (60 items, 120 min, contiguous scenarios, unique items), no keys/rationales/explanations in active state, check forbidden without feedback, irreversible assisted classification, first answer frozen and learning answers unscored, multiple-response complete-selection rule and exact-set scoring, server-side expiry and late-answer rejection, background sweep, concurrent double finish (one audit entry), concurrent answers from two tabs, user isolation (404), insufficient questions, profile versioning, question versioning, progress clean vs assisted, account deletion.
- Content: catalog has no key data; bootstrap idempotent; import preview/commit/retry, no resurrection of old versions, inconsistent keys, key changes on identical content, PDF not implemented message, publication gates including language verification, language evidence rules.
- UI (E2E): card hover/focus/tap preview, detail page, magic-link sign-in and reuse refusal, invalid email message, practice correct and incorrect feedback with per-option rationales and links, multiple response waiting for a full selection, simulation with no solution data in DOM or API responses, assisted switch, timer surviving reload, autosave and answer-sheet map, offline retry, finish and result, cross-user isolation, admin access control, coverage, Markdown import preview, no horizontal scroll on mobile.
- Accessibility: axe-core (WCAG 2 A/AA rules) on /, /exams, /exams/CCDV-F, /login, the exam runner (with and without feedback), result, my exams, progress and admin — no serious or critical violations. Keyboard focus styles, skip link, labelled controls, live regions for save state and timer warnings.

## Coverage (backend, from Microsoft.Testing.Extensions.CodeCoverage cobertura report)
Overall line rate reported by the tool: 95.3 % (branch 79.7 %) including EF migrations. Excluding migrations, computed from the same report:

| File | Lines | Covered | % |
|---|---|---|---|
| ExamPrep.Api/Data/AppDbContext.cs | 228 | 226 | 99.1 |
| ExamPrep.Api/Data/Entities.cs | 194 | 194 | 100.0 |
| ExamPrep.Api/Domain/ExamRules.cs | 371 | 369 | 99.5 |
| ExamPrep.Api/Infrastructure/Infrastructure.cs | 88 | 48 | 54.5 |
| ExamPrep.Api/Modules/AdminEndpoints.cs | 527 | 358 | 67.9 |
| ExamPrep.Api/Modules/AttemptEndpoints.cs | 406 | 396 | 97.5 |
| ExamPrep.Api/Modules/AttemptService.cs | 490 | 464 | 94.7 |
| ExamPrep.Api/Modules/AuthEndpoints.cs | 260 | 220 | 84.6 |
| ExamPrep.Api/Modules/Bootstrap.cs | 305 | 235 | 77.0 |
| ExamPrep.Api/Modules/CatalogEndpoints.cs | 175 | 175 | 100.0 |
| ExamPrep.Api/Modules/ContentImporter.cs | 633 | 605 | 95.6 |
| ExamPrep.Api/Modules/DocumentText.cs | 120 | 112 | 93.3 |
| ExamPrep.Api/Modules/QuestionWorkflow.cs | 148 | 144 | 97.3 |
| ExamPrep.Api/Program.cs | 235 | 227 | 96.6 |
| **Total excluding migrations** | 4180 | 3773 | **90.3** |

Frontend coverage was not measured.

## Security checks
| Check | Command | Result |
|---|---|---|
| .NET vulnerable packages | `dotnet list package --vulnerable --include-transitive` | none |
| npm advisories | `npm audit` | 0 vulnerabilities |
| Secret scan | `gitleaks dir .` and `gitleaks git .` (v8.28.0) | no leaks found |
| Bundle leak probe | grep of a bank rationale and question id in `frontend/dist` | not present |
| Headers | CSP (self only), X-Frame-Options DENY, nosniff, no-referrer, no-store on /api | set in Program.cs |

## Content QA
- Batch 1 (2026-10-03): 142 items; blind solve matched every key (142/142); 9 revised and re-reviewed (9/9 pass).
- Batch 2 (2026-10-05): 189 items; blind solve matched every key (189/189); 11 marked revise plus 5 with length or recall cues were fixed and re-reviewed (15/16 pass in cycle 2, the last one passed in cycle 3).
- Published: 169 CCDV-F (168 families), 162 CCAR-F (160 families). Details in `docs/bank-coverage.md`.
- Watch item: the correct option is the longest in only 11 % (CCDV-F) of single-answer items, below the 25 % chance level. Writers over-corrected the earlier length bias; future batches should aim for about 25 %.
- Known limits: difficulty is editorial; several items depend on current model behaviour or recent/preview features and need re-checking when the docs change; reviews were done by AI agents, not human subject-matter experts.

## Not tested here
- Docker image build and compose run (no Docker daemon in this workspace; `docker compose config` validated).
- Real SMTP delivery (only the development mailbox; the SMTP sender is implemented with MailKit but not exercised).
- Load and long-running soak tests.
