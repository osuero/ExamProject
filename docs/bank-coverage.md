# Bank coverage

Real inventory on 2026-10-05 (latest version of each question). Editorial goal: 300 approved per exam (a product goal, not an official requirement). "Simulation draws" = items per domain in one full-length attempt (largest remainder over the official weights).

## CCDV-F: 169 published (168 distinct families) of 300 target (56%)

| Domain | Weight | Published | Multiple response | Simulation draws | Target |
|---|---|---|---|---|---|
| D1 Agents and Workflows | 14.7% | 25 | 4 | 8 | 44 |
| D2 Applications and Integration | 33.1% | 54 | 10 | 17 | 99 |
| D3 Claude Code | 3.1% | 6 | 0 | 2 | 9 |
| D4 Eval, Testing, and Debugging | 2.6% | 6 | 0 | 1 | 8 |
| D5 Model Selection and Optimization | 16.8% | 29 | 3 | 9 | 51 |
| D6 Prompt and Context Engineering | 11.0% | 18 | 3 | 6 | 33 |
| D7 Security and Safety | 8.1% | 14 | 3 | 4 | 24 |
| D8 Tools and MCPs | 10.6% | 17 | 6 | 6 | 32 |

- Single answer 140, multiple response 29.
- Correct option is the longest in 16 of 140 single-answer items (11%) and the shortest in 28 (20%); chance ≈ 25% each.
- Stored key letters: {'A': 46, 'B': 47, 'C': 46, 'D': 45, 'E': 14} (option order is also shuffled per attempt).
- Review records: {'QA-B1-2026-10-03': 71, 'QA-B2-2026-10-05': 98}.

## CCAR-F: 162 published (160 distinct families) of 300 target (54%)

| Domain | Weight | Published | Multiple response | Simulation draws | Target |
|---|---|---|---|---|---|
| A1 Agentic Architecture & Orchestration | 27% | 46 | 7 | 16 | 81 |
| A2 Tool Design & MCP Integration | 18% | 25 | 4 | 11 | 54 |
| A3 Claude Code Configuration & Workflows | 20% | 34 | 6 | 12 | 60 |
| A4 Prompt Engineering & Structured Output | 20% | 34 | 3 | 12 | 60 |
| A5 Context Management & Reliability | 15% | 23 | 5 | 9 | 45 |

- Single answer 137, multiple response 25.
- Correct option is the longest in 21 of 137 single-answer items (15%) and the shortest in 27 (20%); chance ≈ 25% each.
- Stored key letters: {'A': 45, 'B': 44, 'C': 43, 'D': 44, 'E': 11} (option order is also shuffled per attempt).
- Review records: {'QA-B1-2026-10-03': 71, 'QA-B2-2026-10-05': 91}.
- Items per scenario: {'ARCH-CASE-01': 24, 'ARCH-CASE-02': 29, 'ARCH-CASE-03': 25, 'ARCH-CASE-04': 24, 'ARCH-CASE-05': 32, 'ARCH-CASE-06': 28}. 14 of the 15 four-scenario subsets meet every domain target exactly; the remaining one is filled by redistribution.

## How the bank was built
1. Batch 1 (2026-10-03): 24 original seeds rewritten (v2) and 118 new items; blind review agreed with every key (142/142); 9 revised and re-reviewed.
2. Batch 2 (2026-10-05): 189 new items prioritising the largest gaps (CCDV-F D2, D5, D1; CCAR-F A1, A3, A4) with per-scenario quotas for CCAR-F; blind review agreed with every key (189/189); 16 items revised (11 flagged, 5 with length or recall cues) and re-reviewed, one needed a third cycle.
3. Review records in `content/reviews/` bind each approval to the reviewed content hash; publication passed every gate (validation, reachable sources, verified language).

Families: CCDV-F-B2-D3-001 shares a family with CCDV-F-B2-D2-031, and two CCAR-F items share one with CCAR-F-B1-A2-003, because they test the same concept; an attempt draws at most one item per family.

Difficulty is an editorial estimate, not psychometric calibration. Items that depend on current model behaviour or recent features carry `sourceCheckedAt` and must be re-checked when the documentation changes.
