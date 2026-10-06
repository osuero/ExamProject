# API contract

JSON over HTTPS, same origin as the SPA. Unsafe methods require header `X-XSRF-TOKEN` (value of cookie `XSRF-TOKEN`, issued by `GET /api/auth/csrf` and refreshed after sign-in/out). Errors: `{ "error": "<code>", "message": "<text>", "details": … }`.

## Auth
| Method | Path | Notes |
|---|---|---|
| GET | /api/auth/csrf | issues antiforgery cookies (204) |
| POST | /api/auth/request-link | `{email}` → 202 identical body for any valid address; 400 `invalid_email`; 429 rate limit |
| POST | /api/auth/verify | `{token}` → 200 `{id,email,role}` and session cookie; 400 `invalid_or_expired` |
| POST | /api/auth/logout | 204 |
| GET | /api/auth/me | `{authenticated, id?, email?, role?}` |
| DELETE | /api/me | deletes account and attempts (204) |

## Catalog (public)
| GET | /api/catalog | cards: code, title, level, shortDescription, languages, languageStatus, questionCount, durationMinutes, profileVersion, domains with weights, availability |
|---|---|---|
| GET | /api/catalog/{code} | detail: description, disclaimer, verified languages, profile (version, counts, times, scenariosPerForm, types, pass mark, scoring policy, official score reference, status, sources), domains with objectives and published counts, modes, availability |

## Attempts (authenticated, owner only — other users get 404)
| Method | Path | Body / result |
|---|---|---|
| POST | /api/attempts | `{certificationCode, mode: practice|simulation|custom, feedback?, questionCount?, durationMinutes?, domainCodes?, locale?, preview?}` → 201 `{id}`; 409 `insufficient_questions` (with available/target per domain), `language_pending_verification`; 400 validation codes; 403 `preview_forbidden` |
| GET | /api/attempts?certificationCode= | history summaries |
| GET | /api/attempts/{id} | state: attempt summary, `serverNow`, `remainingSeconds`, items (stem, scenario, options with display labels, selected, flagged, `solution` only when revealed or finished) |
| PUT | /api/attempts/{id}/items/{pos}/answer | `{selected:[ids]}`; after reveal stored as learning answer; 400 `too_many_selections`, `option_unknown`; 409 `attempt_expired`, `attempt_closed` |
| PUT | /api/attempts/{id}/items/{pos}/flag | `{flagged}` |
| POST | /api/attempts/{id}/items/{pos}/check | `{revealWithoutAnswer?}` → item with solution; 409 `feedback_disabled`; 400 `no_answer`, `incomplete_selection` |
| POST | /api/attempts/{id}/feedback | `{enabled}` → `{feedbackEnabled, classification}` (enabling marks assisted for good) |
| POST | /api/attempts/{id}/finish | idempotent; returns summary with score |
| GET | /api/attempts/{id}/result | summary, disclaimer, byDomain (total/correct/points), review items with solutions, toReinforce; 409 while in progress |
| GET | /api/progress?certificationCode= | clean vs assisted aggregates by domain, timeline |

## Admin (role Admin)
| Method | Path |
|---|---|
| GET | /api/admin/questions?certificationCode=&status=&domain= |
| GET | /api/admin/questions/{id} — latest version with key, validation issues, scenario, history |
| POST | /api/admin/questions/{id}/transition — `{to, note?}` with lifecycle gates |
| POST | /api/admin/questions/{id}/versions — edit as new draft version |
| POST | /api/admin/reviews/apply — review record (see content-format.md) |
| GET | /api/admin/coverage?certificationCode= · /api/admin/content-stats?certificationCode= |
| GET | /api/admin/sources · PUT /api/admin/sources/{id} (https URLs only) |
| GET | /api/admin/certifications/{code} — domains, languages, profile versions |
| POST | /api/admin/certifications/{code}/profiles — new profile version |
| POST/DELETE | /api/admin/certifications/{code}/languages[/{locale}] — requires official/provider source and evidence |
| POST | /api/admin/imports/preview · /api/admin/imports/commit — multipart `file` (.json/.md/.docx/.pdf, ≤ 2 MB); report items include `location`; commit is all-or-nothing, 422 with report when invalid |
| GET | /api/admin/imports · /api/admin/audit · /api/admin/users |

## Development only
`GET /api/dev/outbox?to=` exists only in Development and Testing environments.
