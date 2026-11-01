# AI SDLC Assignment — Role-Based Invoice Approval

## Repository and Submission

- Repository: https://github.com/HritikRastogii/ai-sdlc-demo-app
- Feature branch: `feature/role-based-invoice-approval`
- Pull request URL: https://github.com/HritikRastogii/ai-sdlc-demo-app/pull/1
- Demo video: `Demo_Video.mp4` (recorded and uploaded)
- Latest committed revision observed: `d815eeb` (`chore: enable and verify AI workflow hooks`). Separate pre-existing uncommitted source, test, and hook-log changes were present; this documentation update did not alter them, and no final commit is claimed.

## Feature and Business Rules

The feature adds role-based approval to the existing ASP.NET Core invoice application:

- Managers can approve invoices up to and including 50,000.
- Directors can approve invoices up to and including 500,000.
- Invoices above 500,000 require two approvals from distinct Directors. The first approval leaves the invoice pending; the second completes approval.
- Approval identity and role come from the authenticated server-side session, not request-body values.
- Rejected attempts return a clear reason and leave invoice state and approval history unchanged.
- Approval decisions and lifecycle rules are enforced in the Domain layer.

**Implementation assumption:** The assignment explicitly says the second approver for invoices above 500,000 must be a Director, but does not explicitly specify the first approver's role. This implementation requires **both** approvers to be Directors, so a Manager cannot participate in either approval. This is an interpretation, not an additional assignment requirement.

## Implementation Approach and Delivery

Implementation was divided into two separate increments and commits:

1. **Single-approver limits:** enforce the inclusive Manager and Director limits and reject over-limit attempts without mutating invoice state.
2. **Dual approval:** record approvals; require two distinct Directors above 500,000; keep the invoice pending after the first approval and approve it after the second.

The feature keeps business rules in `InvoiceApp.Domain`, obtains approval identity and role from the server-side session, and uses the existing in-memory repository and static UI. Follow-up work added approval-history validation, atomic lifecycle operations, concurrency/snapshot coverage, and the Manager rejection UI scenario.

### Verified Commit History

The following is the branch history checked with `git log` against `main`; no commit or push was performed for this documentation update.

| Commit | Subject |
|---|---|
| `9ebfca3` | chore: add AI SDLC workflow and approval specification |
| `9ddfac4` | feat: enforce role-based invoice approval limits |
| `09bc794` | feat: add dual director approval for high-value invoices |
| `19151d0` | test: cover manager approval limit in Playwright |
| `6af10fc` | test: verify rejected approvals do not persist changes |
| `3861013` | feat: record approval history and validate approvers |
| `44fd9b5` | fix: make invoice lifecycle operations atomic |
| `616c090` | test: verify manager approval rejection through UI |
| `af605a1` | test: cover missing invoices and duplicate approvals |
| `7bf6d24` | docs: update implementation plan with test evidence |
| `d815eeb` | chore: enable and verify AI workflow hooks |

## Verification Results

- **Unit tests:** `dotnet test backend/InvoiceApp.sln --nologo` — **43 passed, 0 failed, 0 skipped** (43 total).
- **Playwright:** `cd e2e && npx playwright test tests/manager-approval-limit.spec.ts` — **1 passed** for the Manager over-limit UI rejection scenario.
- **Whitespace check:** `git diff --check` — passed for the documentation changes in this hand-in update.
- **Hooks:** verified event execution for `sessionStart`, `userPromptSubmitted`, `postToolUse`, and `sessionEnd` against the hook configuration and event log. The log already had uncommitted changes and was left untouched during this documentation update.

### Mutation Testing

Stryker.NET **4.13.0**, using the **JSON reporter**, produced:

| Run | Mutation score |
|---|---:|
| Baseline | 34.69% |
| Final | 60.22% |

The JSON reports are `docs/mutation-reports/baseline-mutation-report.json` and `docs/mutation-reports/improved-mutation-report.json`. Interpret the increase in context: the measured mutant population and coverage changed between runs. The baseline report covers 5 source files and 2 test files (62 mutants: 17 killed, 15 survived, 17 no coverage, 13 ignored); the final report covers 6 source files and 3 test files (126 mutants: 56 killed, 17 survived, 20 no coverage, 29 ignored, 4 compile errors). The scores are useful signals, but not a controlled comparison over an identical set of code and tests.

## Review Findings and Dispositions

The latest review passed after the identified issues were addressed. Earlier review cycles and their outcomes are recorded here; test-coverage limitations that were not part of the requested acceptance checks remain explicitly deferred.

| Finding | Severity | Verdict | Final resolution |
|---|---|---|---|
| Repository reads could expose mutable invoice objects during atomic lifecycle changes. | Medium | **Fixed** | Repository access now returns detached snapshots under the lock; concurrency tests exercise consistent reads. |
| The Manager Playwright test could skip when its password environment variable was absent. | Medium | **Fixed** | The test uses the seeded Manager account by default and checks that a rejected approval leaves invoice data unchanged. |
| Snapshot tests did not cover all read paths, nested approval-history objects, and mutable `Add`/`Update` inputs/results. | Medium | **Fixed** | Tests now cover `GetByIds`, deep-copied approval history, and detached repository values, including mutation of both the original `Add` input and returned value. |
| End-to-end coverage does not exercise the two-Director high-value approval journey or assert the UI progress display after the first approval. | Coverage gap | **Deferred** | The requested Playwright acceptance test is the Manager over-limit rejection; these additional UI scenarios remain future coverage work. |
| There is no dedicated API test attempting to supply a forged approver identity or role. | Coverage gap | **Deferred** | Current approval identity and role are obtained from the server-side session; adding an explicit API-level negative test is useful but was outside the verified test scenarios recorded here. |

No findings were dismissed. Deferred items remain outstanding; the passing review does not mean those additional tests were completed.

## AI Interaction Log and Hooks

`AI-INTERACTION-LOG.md` records the specification and planning work, implementation and test cycles, review rounds, commands, results, and hook verification. It records activities and outcomes only; credentials, tokens, and private reasoning are excluded.

## Submission Checklist

- [x] Pull request created with the implementation commits — https://github.com/HritikRastogii/ai-sdlc-demo-app/pull/1
- [x] Project guidance, agent definitions, and Spec Kit feature artifacts are present and reviewed.
- [x] Unit tests cover the approval rules and reported negative scenarios; the recorded suite result is 43/0/0.
- [x] Playwright Manager over-limit rejection test is included and passed.
- [x] Baseline and final mutation scores are recorded with JSON-report context.
- [x] Review findings and fixed/deferred dispositions are documented.
- [x] AI interaction log records work and verification evidence.
- [x] Record and upload the 8–10 minute demonstration video.
