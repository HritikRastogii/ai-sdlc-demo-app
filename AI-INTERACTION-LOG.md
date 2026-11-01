# AI Interaction Log

This file records AI-assisted development activities and their outcomes.
It must not contain credentials, access tokens, private reasoning, or customer data.

| Date (UTC) | Phase | Tool / Agent | Activity | Outcome / Verification |
|---|---|---|---|---|
| 2026-10-31 | Phase 1 | ChatGPT | Converted the invoice approval assignment into business requirements and acceptance criteria. | Specification created for Manager/Director limits and two-Director approval above 500,000. |
| 2026-10-31 | Phase 1 | Spec Kit CLI | Initialized Spec Kit Copilot integration and generated project infrastructure. | `.specify/` and `.github/skills/` are present; Phase 1 deliverables were committed in `9ebfca3`. |
| 2026-10-31 | Phase 1 | ChatGPT | Prepared feature specification, implementation plan, and task breakdown. | `specs/001-role-based-invoice-approval/` contains spec.md, plan.md, and tasks.md. |
| 2026-10-31 | Phase 1 | ChatGPT | Prepared architect, developer, and reviewer agent instructions and Copilot hook configuration. | Agent definitions and hook configuration are present in the repository. |
| 2026-10-31 | Phase 1 | GitHub Copilot in IntelliJ | Attempted to initialize an agent session. | Blocked by an SDK session authentication/network fetch failure. Work continued without relying on Copilot. |
| 2026-10-31 | Phase 1 | Environment verification | Checked local .NET SDK availability. | .NET SDK was not installed at that time, so testing was deferred; later execution results are recorded below. |
| 2026-11-01 | Phases 2–3 | Developer role / Git history | Implemented the separate single-approver limit and dual-Director approval increments, then added approval-history validation and atomic lifecycle handling. | Commit history confirms the implementation increments (`9ddfac4`, `09bc794`) and follow-up lifecycle work (`3861013`, `44fd9b5`). |
| 2026-11-01 | Phase 4 | Reviewer role | Reviewed the feature branch against `origin/main` and reported inconsistent concurrent reads plus a Playwright test that could skip without an environment password. | Both findings were addressed: repository reads use detached snapshots under lock, and the UI test uses the seeded Manager account by default. |
| 2026-11-01 | Phase 4 | Reviewer role | Reviewed snapshot coverage and identified missing proof for `GetByIds`, nested approval-history copies, and mutable repository inputs/outputs; a later review identified the original `Add` input aliasing case. | Focused tests were added, including mutation of both the caller-owned input and returned `Add` object. The latest review passed. |
| 2026-11-01 | Phase 4 | Developer role | Ran the final recorded unit-test command after the snapshot test updates. | `dotnet test backend/InvoiceApp.sln --nologo` — 43 passed, 0 failed, 0 skipped. |
| 2026-11-01 | Phase 4 | Playwright | Ran the Manager over-limit approval rejection scenario through the UI. | `cd e2e && npx playwright test tests/manager-approval-limit.spec.ts` — 1 passed. |
| 2026-11-01 | Phase 4 | Stryker.NET 4.13.0 | Compared the baseline and final mutation reports using the JSON reporter. | Baseline 34.69%; final 60.22%. Mutant populations and covered files/tests changed; see `ASSIGNMENT.md` for the report counts and comparison context. |
| 2026-11-01 | Phase 4 | Hook verification | Checked configured hooks against the generated event log. | `sessionStart`, `userPromptSubmitted`, `postToolUse`, and `sessionEnd` events are present. The log already had uncommitted changes and was left untouched during this documentation update. |
| 2026-11-01 | Phase 4 | Developer role | Checked whitespace with `git diff --check` while preparing the hand-in documentation. | Passed for the documentation changes. |

## Ongoing Log Instructions

For each significant AI-assisted activity, add a row containing:
- Date
- Assignment phase
- Tool or agent used
- Short description of the task
- Result and how it was verified

Record actual actions and results only. Do not fabricate agent runs, test results, review findings, or mutation scores.
