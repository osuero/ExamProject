# Environment and approval scope

Recorded 2026-10-03. No secret values are stored here.

## Approval given by the owner (chat, 2026-10-03)
- GitHub account `osuero`; repository `osuero/ExamProject` (https://github.com/osuero/ExamProject.git), project name "ExamProject".
- Instruction "procede adelante" after the consolidated environment list: proceed with the work.
- Local folder on the owner's computer: "Exam project" (connected to the session).

Not approved (therefore not done): merge, deploy, hosting provider, paid services, production email provider,
branch-protection changes. These remain the owner's decision.

## Verified with minimal operations
| Item | Check | Result |
|---|---|---|
| Repository exists | `git ls-remote https://github.com/osuero/ExamProject.git` | reachable, empty, public |
| Push access from this session | add repository with push access | **refused: "link your GitHub account to let Claude access repositories"** |
| .NET | `dotnet --version` | 10.0.401 (installed in the cloud workspace with the official install script) |
| Node | `node -v` | v24.21.0 (official tarball, SHA-256 verified) |
| PostgreSQL | local cluster | 16.15 |
| Package registries | npm via the session proxy, NuGet | reachable |
| Docker | CLI present, no daemon | images and compose not run here; `docker compose config` validated |

## Pending owner actions
1. Link GitHub (claude.ai → Settings → Connectors) with the `osuero` account so commits can be pushed and a PR opened.
   The repository is **public**: the question banks with answer keys are in `content/`. Make it private if that is not intended.
2. Decide hosting and database for a deployment (none chosen; no cost incurred).
3. Production email: choose a transactional SMTP provider, verify the sender domain, and set `Email__SmtpHost`,
   `Email__SmtpUser`, `Email__SmtpPassword`, `Email__From` as host secrets. Development uses the local mailbox.
4. Set the first production administrator via `Auth__AdminEmails__0`.
5. Optional: branch protection on `main` with a required reviewer other than the PR author.

## Notes for this sandbox
The session proxy blocks direct connections to `registry.npmjs.org`; npm works when routed through the proxy
(`NO_PROXY` without that host). This is an environment detail of the build workspace, not of the project.
