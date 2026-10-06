# Question bank formats

Banks live in `content/banks/` (backend only). Files are imported in name order at startup (`01-…`, `02-…`, `03-…`), or uploaded in Admin → Import. Imports are previewed, validated and committed all-or-nothing; re-importing identical content is a no-op, and an older file never resurrects an older version. New content always starts as `draft`.

## JSON
```json
{
  "schemaVersion": "1.0",
  "examCode": "CCAR-F",
  "cases": [ { "id": "ARCH-CASE-01", "title": "…", "text": "…" } ],
  "questions": [ {
    "id": "CCAR-F-B1-A1-001", "locale": "en", "domainId": "A1", "objective": "1.1 …",
    "scenarioId": "ARCH-CASE-01", "questionType": "single_choice", "selectCount": 1,
    "stem": "…", "options": [ { "id": "A", "text": "…", "rationale": "…" } ],
    "correctOptionIds": ["B"], "explanation": "…", "points": 1,
    "difficulty": "intermediate", "difficultyBasis": "editorial_estimate_not_calibrated",
    "sourceIds": ["T13"], "sourceCheckedAt": "2026-10-03", "familyId": "CCAR-F-B1-A1-001", "tags": []
  } ]
}
```
Unknown top-level fields (e.g. `newSources`) are ignored by the importer; register sources in `content/sources.json` or Admin → Sources.

## Markdown
```markdown
---
examCode: CCDV-F
---
## Case CASE-ID | Case title
Case text.

## Question CCDV-F-X-001
- domain: D1
- objective: Agent Architecture
- type: single_choice
- select: 1
- scenario: CASE-ID
- sources: T01, T02
- family: CCDV-F-X-001
- difficulty: intermediate

Stem paragraph(s).

### Options
- [ ] A: Option text | rationale: why it does not fit
- [x] B: Option text | rationale: why it is right
- [ ] C: …
- [ ] D: …

### Explanation
General explanation.
```

## Validation (errors block import/advancement)
4–5 options with unique ids and distinct texts; every option has a rationale; key ids exist; `selectCount` equals the key size; single choice selects 1; multiple response selects ≥2 and leaves at least one wrong option; explanation and at least one source; known domain and scenario; no active HTML. Warnings: unregistered source, length cue (correct option clearly longest), near-duplicate (word-shingle Jaccard ≥ 0.6), family id change. A key change on identical stem and options is rejected until resolved with evidence.

## Review records (`content/reviews/*.json`)
```json
{ "reviewId": "QA-B1-2026-10-03", "method": "…", "targetStatus": "published",
  "items": [ { "externalId": "…", "contentHash": "<sha256 of the reviewed version>", "blindAnswerMatchesKey": true, "verdict": "pass", "evidence": "evidence/blind_b1_1_review.json" } ] }
```
A record advances an item through every lifecycle step (each gate still applies) only if the latest version's content hash equals the reviewed hash, the verdict is `pass` and the blind answer matched the key. Evidence files are in `content/reviews/evidence/`.

## Word (.docx) and PDF
Write the bank with the same layout as the Markdown format, one element per paragraph (Word) or line (PDF):
front matter (`---`, `examCode: …`, `---`), `## Case ID | Title`, `## Question ID`, metadata lines (`- domain: …`), stem,
`### Options` with `- [x] B: text | rationale: …`, and `### Explanation`.
Word bullets (•, –) become `- `, and the checkbox glyphs ☒/☑/✔ and ☐ become `[x]` and `[ ]`.

- **Word**: only `word/document.xml` is read; DTDs are prohibited and uncompressed size and compression ratio are limited.
- **PDF**: text is extracted page by page (PdfPig, Apache-2.0), at most 300 pages. Scanned PDFs without a text layer cannot be imported.
- Each question records where it starts (`line N`, `paragraph N` or `page N`). The preview shows it and the stored version keeps it in `Provenance` (`at=…`).
- Validation, duplicates, keys and all-or-nothing commit are identical to the other formats.
