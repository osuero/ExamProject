# Data model (PostgreSQL via EF Core)

Migration: `backend/src/ExamPrep.Api/Data/Migrations/*_Initial.cs`.

| Table | Purpose | Key constraints |
|---|---|---|
| users | account by normalized email, role, verification and last login | unique normalized email |
| login_tokens | magic-link tokens (SHA-256 hash only), expiry, consumption time | unique hash; index (email, created) |
| dev_outbox | development/testing mailbox (not used with SMTP) | — |
| certifications | exam catalog entry (code, title, level, texts, disclaimer) | unique code |
| domains | per-exam domains, editorial bank target, objectives (text[]) | unique (certification, code) |
| verified_languages | locale verified for one exam with source id and evidence | unique (certification, locale) |
| exam_profiles | versioned profile: count, exam time, seat time, types, weights (jsonb), pass mark, scoring policy, verification status, sources | unique (certification, version); one current per exam (partial unique index) |
| sources | source catalog: URL, publisher, kind, confidence, status, dates, conflicts, restrictions | pk id (P01, T13, …) |
| scenarios | shared case text (immutable once imported) | pk id |
| questions | stable identity: external id, certification, family | unique external id; index family |
| question_versions | immutable content: domain, objective, locale, scenario, type, select count, stem, options (jsonb with rationales), key (text[]), explanation, points, difficulty and basis, sources, content hash, lifecycle status and note, provenance, import batch | unique (question, version); index hash, status |
| attempts | owner, exam, profile id/version, mode, preview flag, feedback enabled/exposed, locale, status, start, deadline, finish, profile snapshot (jsonb), pass mark, scoring policy, totals | index (user, started), (status, deadline) |
| attempt_items | position, question version, domain, scenario, option order, selected, scored (frozen), learning, flagged, revealed, correctness | unique (attempt, position) |
| import_batches | file name, format, SHA-256, counts, report (jsonb), actor | — |
| audit_log | actor, action, entity, data (jsonb), time | index time |

Rules enforced in code:
- `Attempt.Classification` = assisted when `FeedbackExposed` (never reset).
- An item's `ScoredOptionIds` is set when its solution is revealed or when the attempt is finalized; scoring uses only it.
- Eligible pool for an attempt = newest version per question in an eligible status and locale, one per family.
- Deleting a user cascades to attempts and items; questions and versions are never deleted (retire instead).
