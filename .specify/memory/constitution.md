# AI SDLC Demo App Constitution

## Core Principles

### I. Domain-Driven Business Rules
Business rules MUST reside in the Domain layer. Controllers and UI components MUST NOT independently implement approval authorization rules.

### II. Test-First Development
Every business rule MUST have automated tests. Tests MUST cover successful scenarios, boundary values, and rejected operations. Negative tests MUST verify that rejected operations leave existing state unchanged.

### III. Layered Architecture
The application MUST preserve separation between Domain, API, persistence, and UI responsibilities. Existing interfaces and dependency injection patterns SHOULD be reused.

### IV. Secure Authorization
Authorization MUST use the authenticated server-side session. Client-supplied roles or identities MUST NOT determine approval authority. Unauthorized operations MUST return clear errors without changing invoice state.

### V. Reviewable Changes
Implementation MUST be delivered in small, independently reviewable commits. Generated tests and implementation MUST be reviewed before acceptance.

## Technical Constraints

- Backend: ASP.NET Core 8 and C#.
- Domain project: `backend/src/InvoiceApp.Domain`.
- API project: `backend/src/InvoiceApp.Api`.
- Tests: xUnit in `backend/tests/InvoiceApp.Tests`.
- UI: existing static application under `backend/src/InvoiceApp.Api/wwwroot`.
- E2E: Playwright under `e2e`.
- Existing repository and authentication patterns SHOULD be preserved.

## Development Workflow

1. Define requirements in a Spec Kit feature specification.
2. Generate an implementation plan and actionable tasks.
3. Implement one independently testable increment at a time.
4. Run unit tests after each increment.
5. Run Playwright E2E tests for the relevant user journey.
6. Perform code review and document findings and dispositions.
7. Record AI-assisted development interactions without recording credentials, tokens, or private reasoning.

## Governance

The constitution applies to all feature implementation and review activities. Any deviation MUST be documented and justified.

**Version**: 1.0.0
**Ratified**: 2026-10-31
**Last Amended**: 2026-10-31
