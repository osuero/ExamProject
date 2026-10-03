# Decisions

Each entry: decision, reason, status. "Official" means supported by the official exam guide or provider pages; everything else is a product decision of this simulator.

## Architecture
1. **Modular monolith**: one ASP.NET Core 10 API with modules (Auth, Catalog, Attempts, Admin, Import, Workflow, Bootstrap) and a pure domain library (`Domain/ExamRules.cs`). The built Angular app is served from the same origin. *Reason*: simple deployment, same-site cookies, no distributed state. *Status*: implemented.
2. **Versions**: .NET 10 (active LTS until 2028-11-14 per Microsoft release metadata), EF Core 10.0.12, Npgsql 10.0.3, Angular 22.2.1 (requires Node ^22.22.3 or ^24.15.0), PostgreSQL 16, Playwright 1.56.1, xUnit v3 4.0.1. All pinned (`global.json`, exact npm versions, NuGet versions). *Checked*: 2026-10-03.
3. **Server-authoritative attempts**: deadline stored at start; every mutation runs in a transaction with `SELECT ... FOR UPDATE` on the attempt row, checks the deadline, and expires the attempt if needed. A background sweep closes abandoned attempts every 15 s. Finish is idempotent.
4. **Immutable snapshots**: attempt stores profile version and a JSON snapshot (weights, allocation, duration, pass mark, scoring policy); items reference question *versions* and a per-attempt option order.
5. **No keys on the client**: the active-attempt DTO omits keys, rationales and explanations unless the item was revealed with feedback enabled. Banks live in `content/` and are never copied to `wwwroot`.
6. **Authentication**: ASP.NET Core cookie authentication + own magic-link tokens: 32 random bytes, SHA-256 hash stored, 15-minute expiry, atomic single-use consumption (`UPDATE ... WHERE ConsumedAt IS NULL`), IP rate limit and per-address throttle, identical responses for any address. The token travels in the URL fragment and is posted by the SPA, so mail scanners and logs do not consume or record it.
7. **CSRF**: antiforgery token in a readable `XSRF-TOKEN` cookie echoed in `X-XSRF-TOKEN` on every unsafe `/api` call (Angular's built-in XSRF support). Auth cookie HttpOnly, SameSite=Lax, Secure outside local development.
8. **Content gating**: publication requires no validation errors, registered and reachable sources, and a verified language for that exam. A review record applies only to the exact content hash it reviewed.

## Product rules (simulator decisions, configurable, not official)
- One point per question; multiple response needs the exact set; no penalty; no partial credit (`simulator_v1_one_point_exact_set_no_penalty`).
- Practice pass mark 80% of points (profile field `SimulatorPassPercent`). The official result is a scaled score 100–1,000 with 720 to pass; it is never converted to a percentage.
- Simulation assembly: largest-remainder allocation by domain weight, one item per family, deficits redistributed and reported, never duplicated; items of a shared scenario are kept together in stable order.
- Feedback after a solution is shown freezes the scored answer; later changes are stored as learning answers and never scored. Enabling feedback in a simulation marks the attempt assisted irreversibly.
- Editorial goal: 300 approved questions per exam (600 total). Current inventory in `docs/bank-coverage.md`.

## Content and evidence
- Exam profiles were checked against the official exam guides v1.0 (P04 CCDV-F, P05 CCAR-F) and the Partner Academy FAQ (P06): counts, 120-minute exam time, domains and weights, question types, 720/1000 scaled score. Status `confirmed_official_exam_guide`.
- Language: the FAQ (P06) states "The exam and prep content are available in English only". English is verified for both exams with that evidence. No other language is enabled.
- CCAR-F official structure (4 of 6 scenarios per exam) is recorded in the profile notes; the assembler does not yet sample scenarios that way (pending item).
- `fuentes.json`, `perfiles_examen.json` and `validacion_semillas.json` were not provided. Sources were rebuilt from verifiable URLs; the T-id mapping for the original seeds is a reconstruction noted on each source.
- The guide sample questions are not copied. The six scenario archetypes are public in the guide; their case texts here are original.
- Validation allows XML-style tags in text (prompt-engineering content such as `<example>` tags) but rejects active HTML (script, img, event handlers, javascript: URLs); the UI renders all content as escaped text.

## Not implemented (designed only)
- PDF and DOCX import: the importer rejects them with a clear "not implemented" message. Extension point: add a parser producing `BankFile` in `ContentImporter.Parse`.
- URL import: not offered, so no server-side fetching (no SSRF surface).
