# Feature Specification: Role-Based Invoice Approval

**Feature Branch**: `001-role-based-invoice-approval`

**Created**: 2026-10-31

**Status**: Draft

**Input**: Implement role-based invoice approval with Manager and Director limits and dual approval for invoices above 500,000.

## User Scenarios & Testing

### User Story 1 - Single approval based on role (Priority: P1)

As a Manager or Director, I want to approve invoices within my authorization limit so that invoices are approved according to organizational approval policies.

**Why this priority**: This is the foundation of role-based invoice authorization.

**Independent Test**: Sign in as Manager and Director and attempt approvals at and beyond their respective limits.

**Acceptance Scenarios**:

1. **Given** a pending invoice of 50,000 or less, **When** a Manager approves it, **Then** the invoice is approved.
2. **Given** a pending invoice above 50,000 and up to 500,000, **When** a Manager attempts approval, **Then** the request is rejected with a clear reason and the invoice remains unchanged.
3. **Given** a pending invoice above 50,000 and up to 500,000, **When** a Director approves it, **Then** the invoice is approved.
4. **Given** a pending invoice of 500,000 or less, **When** a Director approves it, **Then** the invoice is approved.
5. **Given** a pending invoice above 500,000, **When** one Director attempts to approve it as a single approval, **Then** the invoice is not approved and remains pending.

### User Story 2 - Dual approval for high-value invoices (Priority: P2)

As an organization, I want invoices above 500,000 to require two distinct Director approvals so that high-value payments receive additional authorization.

**Why this priority**: High-value invoices require stronger controls than ordinary invoices.

**Independent Test**: Submit an invoice above 500,000 and verify that the first Director approval does not approve the invoice, while a second distinct Director approval completes approval.

**Acceptance Scenarios**:

1. **Given** a pending invoice above 500,000, **When** the first Director approves it, **Then** the approval is recorded and the invoice remains pending.
2. **Given** an invoice above 500,000 with one Director approval, **When** a different Director approves it, **Then** the invoice becomes approved.
3. **Given** an invoice above 500,000 with one Director approval, **When** the same Director attempts approval again, **Then** the request is rejected and the invoice remains unchanged.
4. **Given** an invoice above 500,000 with one Director approval, **When** a Manager attempts the second approval, **Then** the request is rejected and the invoice remains unchanged.
5. **Given** an invoice above 500,000, **When** a Manager attempts the first approval, **Then** the request is rejected and the invoice remains unchanged.

### User Story 3 - Clear rejection of unauthorized approval attempts (Priority: P1)

As a user, I want a clear explanation when an approval attempt is rejected so that I understand the authorization requirement.

**Why this priority**: Rejected actions must be understandable and must not corrupt invoice state.

**Independent Test**: Attempt approvals with an insufficient role, duplicate approver, or invalid invoice state and verify the response and unchanged invoice.

**Acceptance Scenarios**:

1. **Given** an invoice outside the user's approval authority, **When** the user attempts approval, **Then** the system returns a clear rejection reason.
2. **Given** an invoice that is not pending approval, **When** a user attempts approval, **Then** the system rejects the operation.
3. **Given** any rejected approval attempt, **When** the invoice is retrieved, **Then** its status, approval records, decision information, and other existing values remain unchanged.

## Edge Cases

- An invoice of exactly 50,000 can be approved by a Manager.
- An invoice of exactly 500,000 can be approved by one Director.
- An invoice of 500,000.01 requires two distinct Director approvals.
- An invoice amount must not be negative or invalid.
- A duplicate approval by the same user must not count twice.
- Concurrent approval attempts must not result in duplicate approval records or premature final approval.
- An already approved, rejected, or draft invoice cannot receive another approval.
- A rejected approval attempt must not add an approval record or change any invoice fields.
- Approval history must identify each approver and their role.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST identify the authenticated user's role as Manager or Director when processing approval requests.
- **FR-002**: The system MUST allow a Manager to approve pending invoices with an amount less than or equal to 50,000.
- **FR-003**: The system MUST reject a Manager's approval attempt when the invoice amount exceeds 50,000.
- **FR-004**: The system MUST allow a Director to approve pending invoices with an amount less than or equal to 500,000.
- **FR-005**: The system MUST require two distinct Director approvals for invoices with an amount greater than 500,000.
- **FR-006**: The system MUST record each successful approval with the approver identity, role, and approval time.
- **FR-007**: The system MUST NOT count the same user more than once toward an invoice's required approvals.
- **FR-008**: The system MUST keep an invoice pending after its first approval when two approvals are required.
- **FR-009**: The system MUST mark an invoice approved only after all required approvals have been received.
- **FR-010**: The system MUST reject approval attempts from users whose role or identity does not satisfy the approval rules.
- **FR-011**: The system MUST return a clear reason for every rejected approval attempt.
- **FR-012**: The system MUST leave invoice state and approval history unchanged after a rejected approval attempt.
- **FR-013**: The system MUST preserve the existing invoice submission and rejection workflows.
- **FR-014**: The system MUST enforce approval rules in the Domain layer, not only in the API or UI.
- **FR-015**: The system MUST expose approval progress to the UI so users can understand whether further approval is required.

### Key Entities

- **Invoice**: Represents an invoice, including amount, status, and approval progress.
- **Approval Record**: Represents one successful approval, including approver identity, role, and timestamp.
- **User**: Represents an authenticated user with a Manager or Director role.

## Success Criteria

### Measurable Outcomes

- **SC-001**: 100% of Manager approval attempts above 50,000 are rejected.
- **SC-002**: 100% of single Director approvals at or below 500,000 follow the defined authorization limits.
- **SC-003**: 100% of invoices above 500,000 require two approvals from distinct Directors.
- **SC-004**: No duplicate user approval can satisfy the two-approver requirement.
- **SC-005**: All rejected approval attempts leave invoice state unchanged.
- **SC-006**: All approval rules have automated unit test coverage, including negative scenarios.
- **SC-007**: A Playwright E2E test demonstrates rejection of an over-limit approval through the UI.

## Assumptions

- All amounts are expressed in the same currency units used by the existing invoice application.
- The existing authentication and session mechanism will be reused.
- The authenticated session role is the source of authority; clients cannot supply or override their own role.
- **Implementation assumption**: For invoices above 500,000, both approvers must be Directors. The assignment explicitly requires the second approver to be a Director but does not state the first approver's role; requiring both to be Directors is the implementation's interpretation and prevents a Manager from participating above the Manager's 50,000 limit.
- The first Director approval for an invoice above 500,000 records approval progress but does not change the invoice status to Approved.
- Existing invoice lifecycle behavior remains intact except where approval behavior is explicitly extended.
- Concurrency protection must prevent two requests from the same user from being counted as two distinct approvals.

## Scope Boundaries

- Invoice creation and submission rules are unchanged.
- Invoice rejection behavior is unchanged.
- External payment processing is out of scope.
- New organizational hierarchy or user-management functionality is out of scope.
