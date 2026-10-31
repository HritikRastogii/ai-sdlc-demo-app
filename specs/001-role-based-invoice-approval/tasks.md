# Tasks: Role-Based Invoice Approval

## Phase 1 — Specification and Project Setup

- [ ] T001 Finalize the feature specification and verify all approval rules.
- [ ] T002 Finalize the implementation plan and document architectural decisions.
- [ ] T003 Update root AGENTS.md with business rules, architecture, test commands, and review expectations.
- [ ] T004 Create architect, developer, and reviewer agent instructions under .github/agents/.
- [ ] T005 Configure hooks for consistent structured output and AI interaction logging.
- [ ] T006 Configure the AI interaction log without recording credentials or private reasoning.
- [ ] T007 Review generated Spec Kit files and commit the Phase 1 deliverables.

## Phase 2 — Single-Approver Limits

- [ ] T008 Inspect existing Invoice, ApprovalService, repository, controller, and authentication implementations.
- [ ] T009 Introduce Manager and Director approval roles using server-side authenticated identity.
- [ ] T010 Enforce the Manager approval limit of 50,000, inclusive.
- [ ] T011 Enforce the Director approval limit of 500,000, inclusive.
- [ ] T012 Return clear errors for unauthorized and over-limit approval attempts.
- [ ] T013 Verify rejected approval attempts leave invoice state unchanged.
- [ ] T014 Add unit tests for Manager and Director approval success and exact boundaries.
- [ ] T015 Add negative unit tests for role-limit violations and unchanged invoice state.
- [ ] T016 Run the unit test suite and review all test assertions.
- [ ] T017 Commit the single-approver implementation separately.

## Phase 3 — Two-Approver Requirement

- [ ] T018 Extend the domain model to record individual approval records.
- [ ] T019 Require two distinct Director approvals for invoices above 500,000.
- [ ] T020 Keep the invoice pending after the first valid Director approval.
- [ ] T021 Approve the invoice only after a second distinct Director approves it.
- [ ] T022 Reject duplicate approval attempts from the same user.
- [ ] T023 Reject Manager approval attempts for invoices above 500,000.
- [ ] T024 Ensure failed second approvals do not mutate invoice status or approval history.
- [ ] T025 Add unit tests for first approval, second approval, and final approval state.
- [ ] T026 Add negative tests for duplicate approvers and non-Director approval attempts.
- [ ] T027 Add tests for invalid invoice states and boundary amounts.
- [ ] T028 Run the unit test suite and review all test assertions.
- [ ] T029 Commit the dual-approval implementation separately.

## Phase 4 — End-to-End Testing, Review, and Submission

- [ ] T030 Update demo users and UI to support Manager and Director roles.
- [ ] T031 Display approval progress for invoices awaiting a second approval.
- [ ] T032 Display clear errors for rejected approval attempts.
- [ ] T033 Add a Playwright E2E test for an over-limit approval rejected through the UI.
- [ ] T034 Run the Playwright E2E test and record its result.
- [ ] T035 Configure and run Stryker.NET mutation testing.
- [ ] T036 Record baseline and final mutation scores in the assignment write-up.
- [ ] T037 Review the complete implementation diff using the reviewer agent or documented manual review.
- [ ] T038 Run the workshop review prompt against the implementation diff.
- [ ] T039 Record every review finding with a fixed, dismissed, or deferred verdict and justification.
- [ ] T040 Complete AI-INTERACTION-LOG.md.
- [ ] T041 Complete root-level ASSIGNMENT.md with commits, test results, mutation scores, review findings, and video link.
- [ ] T042 Record the 8–10 minute demonstration video.
- [ ] T043 Upload the video and add its link to ASSIGNMENT.md.
- [ ] T044 Verify all hand-in requirements and push the completed feature branch.
