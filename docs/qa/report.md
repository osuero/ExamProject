# QA report

Run on 2026-10-03 in the cloud workspace (Linux x64, .NET SDK 10.0.401, Node 24.21.0, PostgreSQL 16.15, Chromium 1194). Raw outputs are next to this file.

## Automated tests (all executed, real results)
| Suite | Command | Result | Output |
|---|---|---|---|
| Backend unit + integration (real PostgreSQL, real content bootstrap) | `cd backend && dotnet test -- --coverage --coverage-output-format cobertura` | **52 passed, 0 failed** | backend-tests-output.txt |
| Frontend unit (Vitest via Angular) | `cd frontend && npx ng test --watch=false` | **3 passed** | frontend-unit-output.txt |
| Frontend production build | `cd frontend && npx ng build` | success, initial bundle 288.8 kB raw / 79.6 kB transfer | frontend-build-output.txt |
| End-to-end (Playwright, desktop + mobile, axe-core) | `cd frontend && npx playwright test` | **13 passed, 0 failed** | e2e-output.txt |

### What the suites cover
- Auth: identical request-link responses, concurrent single-use tokens, expiry, hashed storage, CSRF required, admin/student/anonymous access.
- Attempts: simulation profile (60 items, 120 min, contiguous scenarios, unique items), no keys/rationales/explanations in active state, check forbidden without feedback, irreversible assisted classification, first answer frozen and learning answers unscored, multiple-response complete-selection rule and exact-set scoring, server-side expiry and late-answer rejection, background sweep, concurrent double finish (one audit entry), concurrent answers from two tabs, user isolation (404), insufficient questions, profile versioning, question versioning, progress clean vs assisted, account deletion.
- Content: catalog has no key data; bootstrap idempotent; import preview/commit/retry, no resurrection of old versions, inconsistent keys, key changes on identical content, PDF not implemented message, publication gates including language verification, language evidence rules.
- UI (E2E): card hover/focus/tap preview, detail page, magic-link sign-in and reuse refusal, invalid email message, practice correct and incorrect feedback with per-option rationales and links, multiple response waiting for a full selection, simulation with no solution data in DOM or API responses, assisted switch, timer surviving reload, autosave and answer-sheet map, offline retry, finish and result, cross-user isolation, admin access control, coverage, Markdown import preview, no horizontal scroll on mobile.
- Accessibility: axe-core (WCAG 2 A/AA rules) on /, /exams, /exams/CCDV-F and /login — no serious or critical violations. Keyboard focus styles, skip link, labelled controls, live regions for save state and timer warnings. Exam runner and admin pages were not scanned with axe.

## Coverage (backend, from Microsoft.Testing.Extensions.CodeCoverage cobertura report)
Overall line rate reported by the tool: 93.7 % (branch 78.3 %) including EF migrations. Excluding migrations, computed from the same report:

| File | Lines | Covered | % |
|---|---|---|---|
| ExamPrep.Api/Data/AppDbContext.cs | 228 | 226 | 99.1 |
| ExamPrep.Api/Data/Entities.cs | 194 | 194 | 100.0 |
| ExamPrep.Api/Domain/ExamRules.cs | 313 | 313 | 100.0 |
| ExamPrep.Api/Infrastructure/Infrastructure.cs | 88 | 48 | 54.5 |
| ExamPrep.Api/Modules/AdminEndpoints.cs | 525 | 356 | 67.8 |
| ExamPrep.Api/Modules/AttemptEndpoints.cs | 406 | 396 | 97.5 |
| ExamPrep.Api/Modules/AttemptService.cs | 484 | 458 | 94.6 |
| ExamPrep.Api/Modules/AuthEndpoints.cs | 260 | 220 | 84.6 |
| ExamPrep.Api/Modules/Bootstrap.cs | 249 | 205 | 82.3 |
| ExamPrep.Api/Modules/CatalogEndpoints.cs | 175 | 89 | 50.9 |
| ExamPrep.Api/Modules/ContentImporter.cs | 610 | 584 | 95.7 |
| ExamPrep.Api/Modules/QuestionWorkflow.cs | 148 | 144 | 97.3 |
| ExamPrep.Api/Program.cs | 235 | 227 | 96.6 |
| **Total excluding migrations** | 3915 | 3460 | **88.4** |

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
- 24 original seeds: blind review found strawman distractors and length cues (10 of 12 CCAR-F keys were the longest option). All 24 rewritten (v2).
- 118 new items authored from the official guides' task statements; facts checked in official docs.
- Independent blind solve of all 142 items: **142/142 matched the key**; 133 pass, 9 revise. The 9 were fixed and re-reviewed blind: **9/9 pass**. Evidence: `content/reviews/evidence/`.
- Published: 71 CCDV-F, 71 CCAR-F. Details in `docs/bank-coverage.md`.
- Known limits: difficulty is editorial (not calibrated); several items depend on current model behaviour or recent features (Opus/Sonnet 5.5 thinking and tool_choice rules, Managed Agents beta, hook field names) and need re-checking when the docs change; this review was done by AI agents, not human subject-matter experts.

## Not tested here
- Docker image build and compose run (no Docker daemon in this workspace; `docker compose config` validated).
- GitHub Actions workflow (repository not linked to the session).
- Real SMTP delivery (only the development mailbox; the SMTP sender is implemented with MailKit but not exercised).
- Load and long-running soak tests.
