# Project conventions for AI agents

Invoice approval demo app: ASP.NET Core 8 Web API with a small static web UI.

## Layout

- `backend/src/InvoiceApp.Domain`: business rules, no web or storage code. `ApprovalService` owns the invoice lifecycle (Draft, PendingApproval, Approved, Rejected).
- `backend/src/InvoiceApp.Api`: controllers, sign-in (`Auth/`), in-memory repository, and the web UI under `wwwroot/`.
- `backend/tests/InvoiceApp.Tests`: xUnit tests, one test class per class under test, named `<ClassUnderTest>Tests`.
- `e2e`: Playwright end-to-end tests (`tests/`) and page objects (`pages/`).
- `docs/use-cases`: short briefs for features we may build.

## Conventions

- Controllers live in `Controllers/`, one per resource, route `api/[controller]`. Request bodies are `record` types declared inside the controller.
- Controllers inherit `ApiControllerBase` and use `Handle(...)` so service errors map to 404, 409 and 400.
- Every endpoint is behind sign-in. Restrict actions with `[RequireRole("...")]`: destructive actions are Admin only, approvals use Manager and Director roles. Admin access does not bypass Domain approval limits.
- Business rules go in the Domain project, never in controllers.
- Register services in `Program.cs`. Repositories are interfaces in Domain with an in-memory implementation in Api.
- Every new rule or endpoint needs tests. Use `FakeInvoiceRepository` in tests.
- Use clear names; no one-letter variables or `flag` parameters.
- End-to-end tests use page objects and role-based locators (`getByRole`, `getByLabel`), never CSS or XPath selectors.

## Commands

- Run the app: `dotnet run --project backend/src/InvoiceApp.Api` then open http://localhost:5000
- Run the tests: `dotnet test backend/InvoiceApp.sln`
- Run end-to-end tests: `cd e2e && npm install && npx playwright install chromium && npm test`

## Demo accounts (not real credentials)

- Configure demo Manager and Director accounts for approval testing. Never trust client-supplied roles.


## Never

- Commit real credentials, tokens or customer data.
- Add business logic to `Program.cs` or controllers.

## Role-Based Invoice Approval Rules

- Manager can approve invoices with an amount less than or equal to 50,000.
- Director can approve invoices with an amount less than or equal to 500,000.
- Invoices above 500,000 require two approvals from distinct Director users.
- The first valid Director approval must leave a high-value invoice pending.
- The second distinct Director approval completes the approval.
- The same authenticated user must never count as two approvers.
- Approval identity and role must come from the authenticated server-side session.
- Rejected approval attempts must return a clear reason and leave invoice status and approval history unchanged.
- Keep all approval authorization and lifecycle rules in the Domain layer.
- Do not trust role or identity values supplied in request bodies.

## Required Test Coverage

- Test Manager and Director approval limits, including exact boundary values.
- Test over-limit attempts for each role.
- Test that failed attempts do not mutate invoice state or approval history.
- Test first and second Director approvals for invoices above 500,000.
- Test duplicate approvers and non-Director participation in high-value approval.
- Test invalid invoice states and invalid amounts.
- Use xUnit and the existing FakeInvoiceRepository patterns.
- Run `dotnet test backend/InvoiceApp.sln` after each implementation increment.

## AI-Assisted Development and Review

- Follow the feature specification, implementation plan, and task list under `specs/001-role-based-invoice-approval/`.
- Keep the single-approver and dual-approval implementations in separate commits.
- Use the architect agent for design decisions, developer agent for implementation, and reviewer agent for independent review.
- Review generated code and tests; do not accept generated output without verification.
- Use structured sections in agent responses: Summary, Changes, Tests, Risks, and Follow-up.
- Record AI-assisted activities in `AI-INTERACTION-LOG.md`.
- Record review findings with fixed, dismissed, or deferred verdicts and reasons.
- Never record credentials, access tokens, or private reasoning in logs.
