# Implementation Plan: Role-Based Invoice Approval

## Summary

Implement role-based invoice approval in the existing ASP.NET Core 8 application. Managers can approve invoices up to 50,000. Directors can approve invoices up to 500,000. Invoices above 500,000 require two approvals from distinct Directors.

The implementation will be delivered in two independently reviewable increments to preserve the required commit history.

## Technical Context

- **Language/Version**: C#, .NET 8
- **Backend**: ASP.NET Core 8 Web API
- **Domain**: `backend/src/InvoiceApp.Domain`
- **API**: `backend/src/InvoiceApp.Api`
- **Tests**: xUnit, `backend/tests/InvoiceApp.Tests`
- **UI**: Static HTML, JavaScript and CSS under `backend/src/InvoiceApp.Api/wwwroot`
- **E2E**: Playwright under `e2e`
- **Persistence**: Existing in-memory repository implementation
- **Authentication**: Existing session-based authentication and role authorization
- **Build/Test command**: `dotnet test backend/InvoiceApp.sln`

## Architecture

### Domain Layer

- Keep approval authorization and approval lifecycle rules inside `ApprovalService`.
- Extend the invoice domain model to represent successful approval records.
- Each approval record must contain approver identity, role, and timestamp.
- Prevent duplicate approvals by the same user.
- Keep invoice state unchanged when an approval attempt fails.

### API Layer

- Reuse existing session authentication.
- Obtain approver identity and role from the authenticated server-side session.
- Never trust a role or identity supplied by the client.
- Translate domain errors into clear HTTP responses using existing controller conventions.
- Expose approval progress to the UI.

### UI Layer

- Preserve existing invoice and approval screens.
- Display approval progress for invoices awaiting a second approval.
- Display clear error messages for rejected attempts.
- Do not implement authorization rules exclusively in JavaScript.

### Testing Layer

- Add unit tests for role limits, exact boundaries, dual approvals, duplicate approvers, invalid invoice states, and unchanged state after rejection.
- Add a Playwright test for a rejected over-limit approval through the UI.
- Review generated tests for meaningful assertions rather than relying on test count alone.

## Implementation Sequence

### Increment 1 — Single-Approver Limits

This increment must be committed separately.

1. Introduce Manager and Director approval roles.
2. Enforce the Manager limit of 50,000.
3. Enforce the Director limit of 500,000.
4. Ensure amounts at the exact limits are accepted.
5. Ensure unauthorized attempts return clear errors.
6. Ensure rejected attempts do not mutate invoice state.
7. Add and run unit tests.

**Commit**: `feat: enforce manager and director invoice approval limits`

### Increment 2 — Dual Approval

This increment must be committed separately.

1. Extend the domain model with approval history.
2. Require two distinct Director approvals for invoices above 500,000.
3. Keep the invoice pending after the first Director approval.
4. Approve the invoice after the second distinct Director approval.
5. Reject duplicate approvals from the same user.
6. Reject Manager participation in high-value approval.
7. Add and run positive and negative unit tests.

**Commit**: `feat: require dual director approval for high-value invoices`

## Additional Quality Work

1. Update authentication demo users and UI behavior for Manager and Director roles.
2. Add a Playwright E2E test for an over-limit approval rejection.
3. Run the complete unit test suite.
4. Configure and run Stryker.NET mutation testing.
5. Record mutation scores before and after improvements.
6. Review the final diff using the reviewer agent or documented manual review if Copilot remains unavailable.
7. Record every review finding with a fixed, dismissed, or deferred verdict and justification.
8. Complete the AI interaction log.
9. Complete root-level `ASSIGNMENT.md`.
10. Record and link the 8–10 minute demonstration video.

## Compatibility and Constraints

- Preserve existing invoice submission and rejection behavior.
- Preserve existing Domain/API separation and repository abstractions.
- Avoid unrelated changes to bulk and partial approval use cases.
- Do not add external payment processing.
- Do not store credentials or tokens in source files or AI logs.
- Protect against duplicate approvals and premature approval when requests occur concurrently.

## Risks

- Existing `DecidedBy` and `DecidedAt` fields represent one approver and may need compatibility treatment when adding approval history.
- The in-memory repository must correctly preserve updated invoice state.
- API role authorization and Domain business authorization must remain consistent.
- Concurrent requests must not count one user twice or approve an invoice prematurely.
- The development environment uses .NET SDK 8.0.427.
- The recorded final backend unit-test run passed 43 tests, with 0 failures and 0 skipped tests.
- The Manager over-limit Playwright UI test passed once.
- Stryker.NET 4.13.0 JSON reports record a baseline score of 34.69% and a final score of 60.22%. The mutant population and coverage changed between runs, so the scores are not a controlled comparison over an identical set of code and tests.
- The configured hooks were verified for `sessionStart`, `userPromptSubmitted`, `postToolUse`, and `sessionEnd` using the hook configuration and event log.
- The latest review passed after the identified repository-snapshot and test-coverage findings were addressed. Additional high-value approval UI and API-authentication test scenarios remain deferred.