# AI SDLC Assignment — Role-Based Invoice Approval

## Repository and Pull Request
- Fork: https://github.com/HritikRastogii/ai-sdlc-demo-app
- Feature branch: `feature/role-based-invoice-approval`
- Pull request: TODO
- Final commit hash: TODO

## Feature Summary
- Manager approval limit: 50,000 inclusive.
- Director approval limit: 500,000 inclusive.
- Above 500,000: two distinct Director approvals.
- Rejected attempts return a clear reason and do not mutate invoice state.

## Phase 1 — Specification and Project Setup
- [ ] AGENTS.md updated.
- [ ] Architect, developer, and reviewer agents added.
- [ ] Hooks configured and validated.
- [ ] Spec Kit specification, plan, and tasks committed.
- [ ] AI interaction log started.
- Phase 1 commit: TODO

## Phase 2 — Single-Approver Limits
- [ ] Manager and Director limits implemented.
- [ ] Boundary and negative unit tests added.
- [ ] Rejected attempts verified not to mutate invoice state.
- [ ] Unit tests executed successfully.
- Commit: TODO
- Test command and result: TODO

## Phase 3 — Dual Approval
- [ ] Two distinct Director approvals implemented above 500,000.
- [ ] First approval leaves invoice pending.
- [ ] Duplicate and unauthorized approvers rejected.
- [ ] Positive and negative unit tests added.
- [ ] Unit tests executed successfully.
- Commit: TODO
- Test command and result: TODO

## Phase 4 — Quality and Submission
### Playwright
- Test scenario: TODO
- Command: TODO
- Result: TODO

### Mutation Testing
- Tool/version: Stryker.NET TODO
- Baseline mutation score: TODO
- Final mutation score: TODO
- Improvements made: TODO

### Code Review
| Finding | Severity | File/Line | Verdict (Fixed/Dismissed/Deferred) | Reason |
|---|---|---|---|---|
| TODO | TODO | TODO | TODO | TODO |

### AI Interaction Log
- File: `AI-INTERACTION-LOG.md`
- Hook event log: `logs/ai-hook-events.jsonl` (if generated)

## Demonstration Video
- Duration: 8–10 minutes
- Video link: TODO

## Known Limitations
Document any blocked or incomplete work honestly. Do not claim successful tests, mutation scores, or reviews without execution evidence.

## Final Submission Checklist
- [ ] Pull request contains at least three standalone commits.
- [ ] AGENTS.md updated.
- [ ] Spec Kit files committed.
- [ ] Unit tests cover all approval rules and negative scenarios.
- [ ] Playwright rejected over-limit E2E test included.
- [ ] Before/after mutation scores recorded.
- [ ] Review findings and verdicts documented.
- [ ] AI interaction log complete.
- [ ] Demo video uploaded and linked.
