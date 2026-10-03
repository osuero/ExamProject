# Bank coverage

Real inventory on 2026-10-03 (latest version of each question). Editorial goal: 300 approved per exam; this is a product goal, not an official requirement. Simulation column = items the simulator draws per domain for one full-length attempt (largest remainder over the official weights).

## CCDV-F — 71 published of 300 target (24%)

| Domain | Weight | Published | Multiple response | Simulation draws | Target |
|---|---|---|---|---|---|
| D1 Agents and Workflows | 14.7% | 10 | 1 | 8 | 44 |
| D2 Applications and Integration | 33.1% | 21 | 5 | 17 | 99 |
| D3 Claude Code | 3.1% | 4 | 0 | 2 | 9 |
| D4 Eval, Testing, and Debugging | 2.6% | 4 | 0 | 1 | 8 |
| D5 Model Selection and Optimization | 16.8% | 11 | 1 | 9 | 51 |
| D6 Prompt and Context Engineering | 11.0% | 8 | 2 | 6 | 33 |
| D7 Security and Safety | 8.1% | 6 | 1 | 4 | 24 |
| D8 Tools and MCPs | 10.6% | 7 | 3 | 6 | 32 |

- Single answer 58, multiple response 13.
- Correct option is the longest in 7 of 58 single-answer items (12%; chance ≈ 25%). It is the shortest in 13 (22%), so length gives no usable cue in either direction.
- Stored key letters: {'A': 19, 'B': 20, 'C': 20, 'D': 18, 'E': 7} (option order is also shuffled per attempt).
- Independently reviewed (blind solve agreed with key, verdict pass): 71 of 71.

## CCAR-F — 71 published of 300 target (24%)

| Domain | Weight | Published | Multiple response | Simulation draws | Target |
|---|---|---|---|---|---|
| A1 Agentic Architecture & Orchestration | 27% | 19 | 2 | 16 | 81 |
| A2 Tool Design & MCP Integration | 18% | 13 | 3 | 11 | 54 |
| A3 Claude Code Configuration & Workflows | 20% | 14 | 2 | 12 | 60 |
| A4 Prompt Engineering & Structured Output | 20% | 14 | 1 | 12 | 60 |
| A5 Context Management & Reliability | 15% | 11 | 2 | 9 | 45 |

- Single answer 61, multiple response 10.
- Correct option is the longest in 10 of 61 single-answer items (16%; chance ≈ 25%). It is the shortest in 12 (20%).
- Stored key letters: {'A': 19, 'B': 20, 'C': 17, 'D': 20, 'E': 5} (option order is also shuffled per attempt).
- Independently reviewed (blind solve agreed with key, verdict pass): 71 of 71.

## Status
All listed items are `published`. Earlier versions of the 24 original seeds are kept as `retired` (superseded by v2). Two CCAR-F items share a family with CCAR-F-B1-A2-003 because they test the same pattern, so a CCAR-F attempt can draw at most 69 distinct items today.

## How the bank was built
1. 24 original seeds (provided) reviewed blind; issues found (strawman distractors, length cues).
2. Seeds rewritten as v2; 118 new items authored from the official guide task statements and verified against official documentation.
3. All 142 items solved blind by independent reviewers (answers matched the keys 142/142); 9 marked revise were fixed and re-reviewed blind (9/9 pass).
4. Review record `content/reviews/QA-B1-2026-10-03.json` binds each approval to the reviewed content hash; publication passed every gate (validation, sources, verified language).

Difficulty is an editorial estimate, not psychometric calibration. There is no evidence yet of predictive value for the official exam.
