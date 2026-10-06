# Sources and exam-profile evidence

Checked 2026-10-03. Hierarchy: 1 official exam guide · 2 Pearson VUE / Anthropic pages · 3 official technical docs · 4 community · 5 testimonials (experience only).

## Exam profile fields

### CCDV-F

| Field | Value | Status | Evidence |
|---|---|---|---|
| examCode | CCDV-F | confirmed_official | P01, P04 |
| numberOfQuestions | 53 | confirmed_official | C01, C03, P04 |
| examTimeMinutes | 120 | confirmed_official | C03, P04, P06 |
| seatTimeMinutes | 135 | confirmed_official | P06, P09 |
| accommodations | Mentioned: reasonable accommodations via Pearson VUE, must be approved before scheduling | confirmed_official | P04, P06, P08, P10 |
| questionTypes | ['multiple-choice', 'multiple-response'] | confirmed_official | P04, P06 |
| optionsPerQuestion | None | unknown | P04 |
| domains | Agents and Workflows 14.7%; Applications and Integration 33.1%; Claude Code 3.1%; Eval, Testing, and Debugging 2.6%; Model Selection and Optimization 16.8%; Prompt and Context Engineering 11.0%; Security and Safety 8.1%; Tools and MCPs 10.6% | confirmed_official | C01, P04 |
| blueprintVersion | version: 1.0; effective: July 2026; pdfCreationDate: 2026-07-08 | confirmed_official | P04 |
| languages | ['English'] | confirmed_official | P06 |
| scoring | scale: 100–1,000 (scaled); passingScore: 720; statedBy: Anthropic (exam guide; Partner Academy FAQ); method: criterion-referenced; cut score set by formal standard-setting study with SMEs; reporting: pass/fail + scaled score + percent-correct by domain (domain % not used for pass/fail) | confirmed_official | P04, P06 |
| priceUSD | 125 | confirmed_official | P02, P04, P06, P07 |
| delivery | Pearson VUE, online proctored (OnVUE) or test center | confirmed_official | P01, P04 |

### CCAR-F

| Field | Value | Status | Evidence |
|---|---|---|---|
| examCode | CCAR-F | confirmed_official | P01, P05 |
| numberOfQuestions | 60 | confirmed_official | C02, C03, C06, G01, P05 |
| examStructure | 4 scenarios drawn at random from a bank of 6 | confirmed_official | C03, P05 |
| examTimeMinutes | 120 | conflicting | C02, C03, C06, P05, P06 |
| seatTimeMinutes | 135 | confirmed_official | P06, P09 |
| accommodations | Mentioned: reasonable accommodations via Pearson VUE, must be approved before scheduling | confirmed_official | P05, P06, P08, P10 |
| questionTypes | ['multiple-choice', 'multiple-response'] | conflicting | G01, P05, P06 |
| optionsPerQuestion | None | unknown | C03, G01, P05 |
| domains | Agentic Architecture & Orchestration 27%; Tool Design & MCP Integration 18%; Claude Code Configuration & Workflows 20%; Prompt Engineering & Structured Output 20%; Context Management & Reliability 15% | confirmed_official | C02, G01, P05 |
| blueprintVersion | version: 1.0; effective: July 2026; pdfCreationDate: 2026-07-08 | confirmed_official | P05 |
| languages | ['English'] | confirmed_official | P06 |
| scoring | scale: 100–1,000 (scaled); passingScore: 720; statedBy: Anthropic (exam guide; Partner Academy FAQ); method: criterion-referenced; cut score set by formal standard-setting study with SMEs; scaled scoring equates across forms; reporting: pass/fail + scaled score + percent-correct by domain | confirmed_official | G01, P05, P06 |
| priceUSD | 125 | confirmed_official | G01, P03, P05, P06, P07 |
| delivery | Pearson VUE, online proctored (OnVUE) or test center | confirmed_official | C03, P05 |

## Conflicts and limitations

- CCAR-F exam time: official sources state 120 minutes; testimony C06 mentions 2.5 hours (likely seat time). Value used: 120.
- CCAR-F format: unofficial guide G01 (May 2026, before guide v1.0) says one correct answer plus three distractors; official sources say multiple-choice and multiple-response. Both types are supported; no official split is published, so none is enforced.
- Options per item are not officially published (guide samples show four). The platform supports four or five.
- Cancellation deadline: guides say 24 hours, FAQ and Pearson page say 48 hours (not used by the platform).
- Blocked or unread: C05 (Reddit testimony) and P11 (Pearson exam list behind sign-in).
- T-ids for the original seeds were reconstructed from topics because `fuentes.json` was not provided.

## Source catalog

169 sources. T-ids above T100 were added while authoring and reviewing batches 1 and 2 (checked 2026-10-03/05).

| Id | Kind | Confidence | Status | Title |
|---|---|---|---|---|
| C01 | community | medium | reachable | [CCDV-F – Claude Certification Guide](https://claudecertificationguide.com/ccdv-f) |
| C02 | community | medium | reachable | [Architect Foundations – Claude Certification Guide](https://claudecertificationguide.com/architect-foundations) |
| C03 | testimony | medium | reachable | [The Claude certification exams: an honest review](https://www.linkedin.com/pulse/claude-certification-exams-honest-review-matthew-purcell-byo2e) |
| C05 | testimony | low | blocked | [Passed the Claude Certified Developer Foundations (r/ClaudeAI)](https://www.reddit.com/r/ClaudeAI/comments/1vzp37x/passed_the_claude_certified_developer_foundations/) |
| C06 | testimony | low | reachable | [How I got Claude certified (and how you can too)](https://rogs.me/2026/07/how-i-got-claude-certified-and-how-you-can-too/) |
| G01 | community | low | reachable | [Exam Overview – CCA-F unofficial study guide](https://ccaf-exam.guide/docs/01-exam-overview/) |
| G02 | community | low | unverified | [CCAF exam guide (unofficial): 02-agentic-loops](https://ccaf-exam.guide/docs/02-agentic-loops/) |
| G03 | community | low | unverified | [CCAF exam guide (unofficial): 03-multi-agent-orchestration](https://ccaf-exam.guide/docs/03-multi-agent-orchestration/) |
| G04 | community | low | unverified | [CCAF exam guide (unofficial): 04-hooks-decomposition-sessions](https://ccaf-exam.guide/docs/04-hooks-decomposition-sessions/) |
| G05 | community | low | unverified | [CCAF exam guide (unofficial): 05-tool-design](https://ccaf-exam.guide/docs/05-tool-design/) |
| G06 | community | low | unverified | [CCAF exam guide (unofficial): 06-mcp-integration](https://ccaf-exam.guide/docs/06-mcp-integration/) |
| G07 | community | low | unverified | [CCAF exam guide (unofficial): 07-claude-md-and-rules](https://ccaf-exam.guide/docs/07-claude-md-and-rules/) |
| G08 | community | low | unverified | [CCAF exam guide (unofficial): 08-commands-skills-plan-mode](https://ccaf-exam.guide/docs/08-commands-skills-plan-mode/) |
| G09 | community | low | unverified | [CCAF exam guide (unofficial): 09-cicd-integration](https://ccaf-exam.guide/docs/09-cicd-integration/) |
| G10 | community | low | unverified | [CCAF exam guide (unofficial): 10-prompt-engineering](https://ccaf-exam.guide/docs/10-prompt-engineering/) |
| G11 | community | low | unverified | [CCAF exam guide (unofficial): 11-structured-output-and-batch](https://ccaf-exam.guide/docs/11-structured-output-and-batch/) |
| G12 | community | low | unverified | [CCAF exam guide (unofficial): 12-context-and-reliability](https://ccaf-exam.guide/docs/12-context-and-reliability/) |
| P01 | provider | high | reachable | [Claude Certification Program by Anthropic - Pearson VUE](https://www.pearsonvue.com/us/en/anthropic.html) |
| P02 | provider | high | reachable | [Claude Certified Developer – Foundations Certification](https://anthropic-partners.skilljar.com/claude-certified-developer-foundations-certification) |
| P03 | provider | high | reachable | [Claude Certified Architect – Foundations Certification](https://anthropic-partners.skilljar.com/claude-certified-architect-foundations-certification) |
| P04 | official_exam | high | reachable | [Claude Certified Developer – Foundations Exam Guide (v1.0)](https://everpath-course-content.s3-accelerate.amazonaws.com/instructor%2F6nizmqk8tpzpfjvt6qmmav7rh%2Fpublic%2F1783542875%2FClaude+Certified+Developer+%E2%80%93+Foundations+Exam+Guide.pdf) |
| P05 | official_exam | high | reachable | [Claude Certified Architect – Foundations Exam Guide (v1.0)](https://everpath-course-content.s3-accelerate.amazonaws.com/instructor%2F6nizmqk8tpzpfjvt6qmmav7rh%2Fpublic%2F1783542750%2FClaude+Certified+Architect+%E2%80%93+Foundations+Exam+Guide.pdf) |
| P06 | provider | high | reachable | [Certification FAQ – Anthropic Partner Academy](https://anthropic-partners.skilljar.com/page/faq-certifications) |
| P07 | provider | high | reachable | [Partner Certifications – Anthropic Partner Academy](https://anthropic-partners.skilljar.com/page/partner-certifications) |
| P08 | provider | high | reachable | [Anthropic Certification Exam Policy](https://everpath-course-content.s3-accelerate.amazonaws.com/instructor%2F34hhd92iyp94a0gtbr15cy5jk%2Fpublic%2F1782870704%2FAnthropic+Certification+Exam+Policy.pdf) |
| P09 | provider | high | reachable | [Anthropic Online Exams with OnVUE](https://www.pearsonvue.com/us/en/anthropic/onvue.html) |
| P10 | provider | high | reachable | [Request test accommodations (Anthropic)](https://www.pearsonvue.com/us/en/test-takers/accommodations/pearson_approve.anthropic.html) |
| P11 | provider | low | blocked | [Pearson VUE Anthropic exam list (WSR)](https://wsr.pearsonvue.com/testtaker/registration/examlist/ANTHROPIC?locale=en_US) |
| T01 | technical_doc | high | reachable | [Building effective agents](https://www.anthropic.com/engineering/building-effective-agents) |
| T02 | technical_doc | high | reachable | [Agent SDK overview](https://code.claude.com/docs/en/agent-sdk/overview) |
| T03 | technical_doc | high | reachable | [How tool use works](https://platform.claude.com/docs/en/agents-and-tools/tool-use/how-tool-use-works) |
| T04 | technical_doc | high | reachable | [Handling stop reasons](https://platform.claude.com/docs/en/build-with-claude/handling-stop-reasons) |
| T05 | technical_doc | high | reachable | [Streaming messages](https://platform.claude.com/docs/en/build-with-claude/streaming) |
| T06 | technical_doc | high | reachable | [Run Claude Code programmatically (headless)](https://code.claude.com/docs/en/headless) |
| T07 | technical_doc | high | reachable | [API errors](https://platform.claude.com/docs/en/api/errors) |
| T08 | technical_doc | high | reachable | [Choosing a model](https://platform.claude.com/docs/en/about-claude/models/choosing-a-model) |
| T09 | technical_doc | high | reachable | [Token counting](https://platform.claude.com/docs/en/build-with-claude/token-counting) |
| T10 | technical_doc | high | reachable | [Structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs) |
| T11 | technical_doc | high | reachable | [Claude Code permissions](https://code.claude.com/docs/en/permissions) |
| T12 | technical_doc | high | reachable | [MCP 2025-06-18 transports](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) |
| T13 | technical_doc | high | reachable | [Hooks reference](https://code.claude.com/docs/en/hooks) |
| T14 | technical_doc | high | reachable | [Reduce hallucinations](https://platform.claude.com/docs/en/test-and-evaluate/strengthen-guardrails/reduce-hallucinations) |
| T15 | technical_doc | high | reachable | [Manage Claude's memory (CLAUDE.md and rules)](https://code.claude.com/docs/en/memory) |
| T16 | technical_doc | high | reachable | [Agent Skills in Claude Code](https://code.claude.com/docs/en/skills) |
| T17 | technical_doc | high | reachable | [Subagents](https://code.claude.com/docs/en/sub-agents) |
| T18 | technical_doc | high | reachable | [Effective context engineering for AI agents](https://www.anthropic.com/engineering/effective-context-engineering-for-ai-agents) |
| T19 | technical_doc | high | reachable | [Writing effective tools for agents](https://www.anthropic.com/engineering/writing-tools-for-agents) |
| T20 | technical_doc | high | reachable | [MCP 2025-06-18 resources](https://modelcontextprotocol.io/specification/2025-06-18/server/resources) |
| T21 | technical_doc | high | reachable | [Use examples (multishot prompting)](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/multishot-prompting) |
| T22 | technical_doc | high | reachable | [Batch processing](https://platform.claude.com/docs/en/build-with-claude/batch-processing) |
| T23 | technical_doc | high | reachable | [MCP 2025-06-18 tools](https://modelcontextprotocol.io/specification/2025-06-18/server/tools) |
| T24 | technical_doc | high | reachable | [Claude Code GitHub Actions](https://code.claude.com/docs/en/github-actions) |
| T101 | technical_doc | high | reachable | [Tool runner (SDK)](https://platform.claude.com/docs/en/agents-and-tools/tool-use/tool-runner) |
| T102 | technical_doc | high | reachable | [Handle tool calls](https://platform.claude.com/docs/en/agents-and-tools/tool-use/handle-tool-calls) |
| T103 | technical_doc | high | reachable | [Intercept and control agent behavior with hooks (Agent SDK)](https://code.claude.com/docs/en/agent-sdk/hooks) |
| T104 | technical_doc | high | reachable | [Configure permissions (Agent SDK)](https://code.claude.com/docs/en/agent-sdk/permissions) |
| T105 | technical_doc | high | reachable | [Code execution tool](https://platform.claude.com/docs/en/agents-and-tools/tool-use/code-execution-tool) |
| T106 | technical_doc | high | reachable | [CLI reference](https://code.claude.com/docs/en/cli-reference) |
| T107 | technical_doc | high | reachable | [MCP 2025-06-18 prompts](https://modelcontextprotocol.io/specification/2025-06-18/server/prompts) |
| T110 | technical_doc | high | reachable | [How we built our multi-agent research system](https://www.anthropic.com/engineering/multi-agent-research-system) |
| T111 | technical_doc | high | reachable | [Intercept and control agent behavior with hooks (Agent SDK)](https://code.claude.com/docs/en/agent-sdk/hooks) |
| T112 | technical_doc | high | reachable | [Parallel tool use](https://platform.claude.com/docs/en/agents-and-tools/tool-use/parallel-tool-use) |
| T113 | technical_doc | high | reachable | [Handle tool calls](https://platform.claude.com/docs/en/agents-and-tools/tool-use/handle-tool-calls) |
| T114 | technical_doc | high | reachable | [Claude Managed Agents overview](https://platform.claude.com/docs/en/managed-agents/overview) |
| T115 | technical_doc | high | reachable | [Self-hosted sandboxes (Managed Agents)](https://platform.claude.com/docs/en/managed-agents/self-hosted-sandboxes) |
| T116 | technical_doc | high | reachable | [Memory tool](https://platform.claude.com/docs/en/agents-and-tools/tool-use/memory-tool) |
| T117 | technical_doc | high | reachable | [Context editing](https://platform.claude.com/docs/en/build-with-claude/context-editing) |
| T118 | technical_doc | high | reachable | [Subagents in the Agent SDK](https://code.claude.com/docs/en/agent-sdk/subagents) |
| T119 | technical_doc | high | reachable | [Claude on Google Cloud](https://platform.claude.com/docs/en/build-with-claude/claude-on-vertex-ai) |
| T120 | technical_doc | high | reachable | [Model deprecations](https://platform.claude.com/docs/en/about-claude/model-deprecations) |
| T121 | technical_doc | high | reachable | [Define success criteria and build evaluations](https://platform.claude.com/docs/en/test-and-evaluate/develop-tests) |
| T122 | technical_doc | high | reachable | [Prompt caching](https://platform.claude.com/docs/en/build-with-claude/prompt-caching) |
| T123 | technical_doc | high | reachable | [Thinking in tool and multi-turn workflows](https://platform.claude.com/docs/en/build-with-claude/thinking-tool-workflows) |
| T124 | technical_doc | high | reachable | [PDF support](https://platform.claude.com/docs/en/build-with-claude/pdf-support) |
| T125 | technical_doc | high | reachable | [Python SDK](https://platform.claude.com/docs/en/cli-sdks-libraries/sdks/python) |
| T126 | technical_doc | high | reachable | [Best practices for Claude Code](https://code.claude.com/docs/en/best-practices) |
| T127 | technical_doc | high | reachable | [System prompts (release notes)](https://platform.claude.com/docs/en/release-notes/system-prompts/overview) |
| T128 | technical_doc | high | reachable | [Prompting best practices](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices) |
| T129 | technical_doc | high | reachable | [Strict tool use](https://platform.claude.com/docs/en/agents-and-tools/tool-use/strict-tool-use) |
| T130 | technical_doc | high | reachable | [Install and manage plugins](https://code.claude.com/docs/en/plugins/install) |
| T131 | technical_doc | high | reachable | [Plugin dependencies](https://code.claude.com/docs/en/plugins/dependencies) |
| T132 | technical_doc | high | reachable | [Model IDs and versioning](https://platform.claude.com/docs/en/about-claude/models/model-ids-and-versions) |
| T133 | technical_doc | high | reachable | [Building with thinking](https://platform.claude.com/docs/en/build-with-claude/thinking) |
| T140 | technical_doc | high | reachable | [Claude Code settings files and precedence](https://code.claude.com/docs/en/settings) |
| T141 | technical_doc | high | reachable | [Claude Code CLI reference](https://code.claude.com/docs/en/cli-reference) |
| T142 | technical_doc | high | reachable | [Handle tool calls](https://platform.claude.com/docs/en/agents-and-tools/tool-use/handle-tool-calls) |
| T143 | technical_doc | high | reachable | [Migrating to Claude Opus 5.5](https://platform.claude.com/docs/en/models/opus-5-5/migration-guide) |
| T144 | technical_doc | high | reachable | [Migrating to Claude Sonnet 5.5](https://platform.claude.com/docs/en/models/sonnet-5-5/migration-guide) |
| T145 | technical_doc | high | reachable | [Effort](https://platform.claude.com/docs/en/build-with-claude/effort) |
| T146 | technical_doc | high | reachable | [Glossary](https://platform.claude.com/docs/en/about-claude/glossary) |
| T147 | technical_doc | high | reachable | [Python SDK](https://platform.claude.com/docs/en/cli-sdks-libraries/sdks/python) |
| T148 | technical_doc | high | reachable | [Prompt caching](https://platform.claude.com/docs/en/build-with-claude/prompt-caching) |
| T149 | technical_doc | high | reachable | [Context editing](https://platform.claude.com/docs/en/build-with-claude/context-editing) |
| T150 | technical_doc | high | reachable | [Prompting best practices](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices) |
| T151 | technical_doc | high | reachable | [Mitigate jailbreaks and prompt injections](https://platform.claude.com/docs/en/test-and-evaluate/strengthen-guardrails/mitigate-jailbreaks) |
| T152 | technical_doc | high | reachable | [Workload Identity Federation](https://platform.claude.com/docs/en/manage-claude/workload-identity-federation) |
| T153 | technical_doc | high | reachable | [Files API](https://platform.claude.com/docs/en/build-with-claude/files) |
| T154 | technical_doc | high | reachable | [Define tools](https://platform.claude.com/docs/en/agents-and-tools/tool-use/define-tools) |
| T155 | technical_doc | high | reachable | [MCP 2025-06-18 server overview](https://modelcontextprotocol.io/specification/2025-06-18/server) |
| T156 | technical_doc | high | reachable | [MCP 2025-06-18 prompts](https://modelcontextprotocol.io/specification/2025-06-18/server/prompts) |
| T170 | technical_doc | high | reachable | [Subagents in the SDK](https://code.claude.com/docs/en/agent-sdk/subagents) |
| T171 | technical_doc | high | reachable | [Intercept and control agent behavior with hooks](https://code.claude.com/docs/en/agent-sdk/hooks) |
| T172 | technical_doc | high | reachable | [Work with sessions](https://code.claude.com/docs/en/agent-sdk/sessions) |
| T173 | technical_doc | high | reachable | [Connect Claude Code to tools via MCP](https://code.claude.com/docs/en/mcp) |
| T174 | technical_doc | high | reachable | [Define tools](https://platform.claude.com/docs/en/agents-and-tools/tool-use/implement-tool-use) |
| T175 | technical_doc | high | reachable | [Handle tool calls](https://platform.claude.com/docs/en/agents-and-tools/tool-use/handle-tool-calls) |
| T176 | technical_doc | high | reachable | [CLI reference](https://code.claude.com/docs/en/cli-reference) |
| T177 | technical_doc | high | reachable | [Tools reference](https://code.claude.com/docs/en/tools-reference) |
| T178 | technical_doc | high | reachable | [How the agent loop works](https://code.claude.com/docs/en/agent-sdk/agent-loop) |
| T179 | technical_doc | high | reachable | [Manage sessions](https://code.claude.com/docs/en/sessions) |
| T180 | technical_doc | high | reachable | [How we built our multi-agent research system](https://www.anthropic.com/engineering/multi-agent-research-system) |
| T200 | technical_doc | high | reachable | [Prompting best practices](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices) |
| T201 | technical_doc | high | reachable | [Claude Code permission modes](https://code.claude.com/docs/en/permission-modes) |
| T202 | technical_doc | high | reachable | [Claude Code CLI reference](https://code.claude.com/docs/en/cli-reference) |
| T203 | technical_doc | high | reachable | [Define tools](https://platform.claude.com/docs/en/agents-and-tools/tool-use/define-tools) |
| T204 | technical_doc | high | reachable | [Claude Code best practices](https://code.claude.com/docs/en/best-practices) |
| T205 | technical_doc | high | reachable | [How we built our multi-agent research system](https://www.anthropic.com/engineering/multi-agent-research-system) |
| T206 | technical_doc | high | reachable | [Claude Code commands](https://code.claude.com/docs/en/commands) |
| T207 | technical_doc | high | reachable | [Claude Code sessions](https://code.claude.com/docs/en/sessions) |
| T208 | technical_doc | high | reachable | [MCP connector](https://platform.claude.com/docs/en/agents-and-tools/mcp-connector) |
| T230 | technical_doc | high | reachable | [Rate limits](https://platform.claude.com/docs/en/api/rate-limits) |
| T231 | technical_doc | high | reachable | [Zero data retention](https://platform.claude.com/docs/en/build-with-claude/zero-data-retention) |
| T232 | technical_doc | high | reachable | [Workspaces](https://platform.claude.com/docs/en/build-with-claude/workspaces) |
| T233 | technical_doc | high | reachable | [Working with the Messages API](https://platform.claude.com/docs/en/build-with-claude/working-with-messages) |
| T234 | technical_doc | high | reachable | [Mid-conversation system messages](https://platform.claude.com/docs/en/build-with-claude/mid-conversation-system-messages) |
| T235 | technical_doc | high | reachable | [Vision](https://platform.claude.com/docs/en/build-with-claude/vision) |
| T236 | technical_doc | high | reachable | [Server tools](https://platform.claude.com/docs/en/agents-and-tools/tool-use/server-tools) |
| T237 | technical_doc | high | reachable | [Claude in Amazon Bedrock (Opus 4.7 and later)](https://platform.claude.com/docs/en/build-with-claude/claude-in-amazon-bedrock) |
| T238 | technical_doc | high | reachable | [Use Claude Code features in the SDK](https://code.claude.com/docs/en/agent-sdk/claude-code-features) |
| T239 | technical_doc | high | reachable | [Checkpointing](https://code.claude.com/docs/en/checkpointing) |
| T240 | technical_doc | high | reachable | [Manage Claude Code plugins for your organization](https://code.claude.com/docs/en/plugins/org) |
| T241 | technical_doc | high | reachable | [Agent Skills overview](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/overview) |
| T242 | technical_doc | high | reachable | [Using Agent Skills with the API](https://platform.claude.com/docs/en/build-with-claude/skills-guide) |
| T243 | technical_doc | high | reachable | [Run parallel sessions with worktrees](https://code.claude.com/docs/en/worktrees) |
| T244 | technical_doc | high | reachable | [Code Review](https://code.claude.com/docs/en/code-review) |
| T246 | technical_doc | high | reachable | [List Message Batches](https://platform.claude.com/docs/en/api/messages/batches/list) |
| T247 | technical_doc | high | reachable | [TypeScript SDK](https://platform.claude.com/docs/en/cli-sdks-libraries/sdks/typescript) |
| T248 | technical_doc | high | reachable | [MCP tunnels](https://platform.claude.com/docs/en/agents-and-tools/mcp-tunnels/overview) |
| T260 | technical_doc | high | reachable | [Fast mode (research preview)](https://platform.claude.com/docs/en/build-with-claude/fast-mode) |
| T261 | technical_doc | high | reachable | [Context windows](https://platform.claude.com/docs/en/build-with-claude/context-windows) |
| T262 | technical_doc | high | reachable | [Steering thinking](https://platform.claude.com/docs/en/build-with-claude/thinking-steering-and-cost) |
| T263 | technical_doc | high | reachable | [Troubleshooting thinking](https://platform.claude.com/docs/en/build-with-claude/thinking-troubleshooting) |
| T264 | technical_doc | high | reachable | [Models overview](https://platform.claude.com/docs/en/models/overview) |
| T265 | technical_doc | high | reachable | [Compaction at a token threshold](https://platform.claude.com/docs/en/build-with-claude/compaction-threshold) |
| T266 | technical_doc | high | reachable | [API overview](https://platform.claude.com/docs/en/api/overview) |
| T267 | technical_doc | high | reachable | [Rate limits](https://platform.claude.com/docs/en/api/rate-limits) |
| T268 | technical_doc | high | reachable | [List Models (API reference)](https://platform.claude.com/docs/en/api/models/list) |
| T269 | technical_doc | high | reachable | [Tool search tool](https://platform.claude.com/docs/en/agents-and-tools/tool-use/tool-search-tool) |
| T270 | technical_doc | high | reachable | [Use Claude Code features in the SDK](https://code.claude.com/docs/en/agent-sdk/claude-code-features) |
| T271 | technical_doc | high | reachable | [Give Claude custom tools (Agent SDK)](https://code.claude.com/docs/en/agent-sdk/custom-tools) |
| T272 | technical_doc | high | reachable | [Modifying system prompts (Agent SDK)](https://code.claude.com/docs/en/agent-sdk/modifying-system-prompts) |
| T273 | technical_doc | high | reachable | [Handle approvals and user input (Agent SDK)](https://code.claude.com/docs/en/agent-sdk/user-input) |
| T274 | technical_doc | high | reachable | [LangGraph interrupts](https://docs.langchain.com/oss/python/langgraph/interrupts) |
| T275 | technical_doc | high | reachable | [Messages API reference](https://platform.claude.com/docs/en/api/messages) |
| T290 | technical_doc | high | reachable | [Compaction at a token threshold](https://platform.claude.com/docs/en/build-with-claude/compaction-threshold) |
| T291 | technical_doc | high | reachable | [Tool search tool](https://platform.claude.com/docs/en/agents-and-tools/tool-use/tool-search-tool) |
| T292 | technical_doc | high | reachable | [MCP 2025-06-18 authorization](https://modelcontextprotocol.io/specification/2025-06-18/basic/authorization) |
| T293 | technical_doc | high | reachable | [Handle approvals and user input (Agent SDK)](https://code.claude.com/docs/en/agent-sdk/user-input) |
| T294 | technical_doc | high | reachable | [Configure the sandboxed Bash tool](https://code.claude.com/docs/en/sandboxing) |
| T295 | technical_doc | high | reachable | [Claude Code settings reference](https://code.claude.com/docs/en/settings-reference) |
| T296 | technical_doc | high | reachable | [Claude Code authentication](https://code.claude.com/docs/en/authentication) |
| T297 | technical_doc | high | reachable | [Usage and Cost API](https://platform.claude.com/docs/en/build-with-claude/usage-cost-api) |
| T298 | technical_doc | high | reachable | [Workspaces](https://platform.claude.com/docs/en/build-with-claude/workspaces) |
| T299 | technical_doc | high | reachable | [Using Agent Skills with the API](https://platform.claude.com/docs/en/build-with-claude/skills-guide) |
| T300 | technical_doc | high | reachable | [Claude Code plugins overview](https://code.claude.com/docs/en/plugins) |
| T350 | technical_doc | high | reachable | [Claude Code error reference](https://code.claude.com/docs/en/errors) |
| T380 | technical_doc | high | reachable | [Manage costs effectively (Claude Code)](https://code.claude.com/docs/en/costs) |
| T381 | technical_doc | high | reachable | [Context windows](https://platform.claude.com/docs/en/build-with-claude/context-windows) |
| T382 | technical_doc | high | reachable | [Working with the Messages API](https://platform.claude.com/docs/en/build-with-claude/working-with-messages) |
