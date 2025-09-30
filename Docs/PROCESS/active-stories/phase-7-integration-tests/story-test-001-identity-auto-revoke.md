# Story: Auto-Revoke Old Wallet Credentials

**Story ID**: TEST-001
**Status**: Draft
**Module**: Identity
**Story Type**: Feature
**Priority**: High
**Complexity**: Medium
**Created**: 2025-09-30
**Last Updated**: 2025-09-30

---

## User Story

**As a** system administrator
**I want** old wallet credentials to be automatically revoked after 90 days of inactivity
**So that** we reduce security risks from unused authentication methods

---

## Technical Context

### Architecture Alignment

**Related Tech Spec**: N/A (Integration Test Story)

**Patterns Required**:
- Result<T, Error> error handling
- StrongId<T> for entity IDs (AxonUserId, AxonUserAuthId)
- CQRS (command/query separation)
- Domain events: WalletCredentialRevokedEvent

**Module Boundaries**:
- Primary Module: Identity
- Dependencies: None (self-contained)

**Relevant ADRs**:
- ADR-003: Result Pattern (error handling)
- ADR-004: Strong IDs (type-safe identifiers)
- ADR-002: CQRS + MediatR (commands/queries)

---

## Acceptance Criteria

**Testable Criteria** (one test per AC):

1. **AC1**: System automatically revokes credentials inactive for 90+ days
   - Test: Given a wallet credential with LastUsedAt = 91 days ago, When auto-revoke job runs, Then credential IsRevoked = true

2. **AC2**: Recently used credentials are not revoked
   - Test: Given a wallet credential with LastUsedAt = 30 days ago, When auto-revoke job runs, Then credential IsRevoked = false

3. **AC3**: Revocation event is raised for auditing
   - Test: Given a credential is auto-revoked, When revocation occurs, Then WalletCredentialRevokedEvent is published with reason "AutoRevoked_Inactive90Days"

4. **AC4**: User can still authenticate with other active credentials
   - Test: Given a user has 2 wallet credentials (1 revoked, 1 active), When user authenticates with active wallet, Then authentication succeeds

---

## Detailed Design

### Services & Components

**Command**: `AutoRevokeInactiveCredentialsCommand`
- Handler: `AutoRevokeInactiveCredentialsCommandHandler`
- Dependencies: `IIdentityWriteRepository`, `IDateTimeProvider`
- Returns: `Result<AutoRevokeResult, Error>`

**Domain Logic**: `AxonUserAuth` aggregate
- Method: `RevokeCredential(string reason)` → `Result<Unit, Error>`
- Business rule: Cannot revoke if it's the user's only authentication method
- Raises: `WalletCredentialRevokedEvent`

### Data Models

**Entity**: `AxonUserAuth`
- Properties:
  - `AxonUserAuthId Id` (StrongId<T>)
  - `AxonUserId UserId` (StrongId<T>)
  - `DateTime? LastUsedAt`
  - `bool IsRevoked`
  - `string? RevocationReason`
  - `DateTime? RevokedAt`

**Domain Event**: `WalletCredentialRevokedEvent`
- Properties:
  - `AxonUserAuthId CredentialId`
  - `AxonUserId UserId`
  - `string Reason`
  - `DateTime RevokedAt`

### APIs/Interfaces

**Command Endpoint**: POST `/api/v1/identity/credentials/auto-revoke` (Admin only)
- Request: `AutoRevokeCredentialsRequest { int InactiveDaysThreshold }`
- Response: `AutoRevokeCredentialsResponse { int TotalRevoked, List<Guid> RevokedCredentialIds }`

**Query Endpoint**: GET `/api/v1/identity/credentials/inactive` (Admin only)
- Query params: `?inactiveDays=90`
- Response: `InactiveCredentialsResponse { List<InactiveCredentialDto> Credentials }`

---

## Dependencies & Integrations

**External Dependencies**:
- Library: MediatR (command handling)
- Library: FluentValidation (request validation)
- Library: EF Core (repository)

**Module Dependencies**:
- None (self-contained within Identity module)

**Database Changes**:
- Migration needed: No (uses existing AxonUserAuths table)
- New columns: None (LastUsedAt, IsRevoked, RevocationReason, RevokedAt already exist)

---

## Test Strategy

### Unit Tests (Domain Layer)
- Test `AxonUserAuth.RevokeCredential()` method
  - Success case: Valid revocation
  - Error case: Cannot revoke if last auth method
  - Verify domain event raised

### Integration Tests (Application Layer)
- Test `AutoRevokeInactiveCredentialsCommandHandler`
  - Test with Testcontainers PostgreSQL
  - Verify correct credentials selected (90+ days)
  - Verify credentials updated in database
  - Verify events published

### E2E Tests (API Layer)
- Test POST `/api/v1/identity/credentials/auto-revoke` endpoint
  - Test admin authorization
  - Test response format
  - Test idempotency

### AC Coverage Tests
- `Given_CredentialInactive91Days_When_AutoRevokeRuns_Then_CredentialRevoked` (AC1)
- `Given_CredentialInactive30Days_When_AutoRevokeRuns_Then_CredentialNotRevoked` (AC2)
- `Given_CredentialRevoked_When_RevocationOccurs_Then_EventPublished` (AC3)
- `Given_UserHas2Credentials_When_OneRevoked_Then_CanAuthenticateWithOther` (AC4)

---

## Risks & Assumptions

### Risks
- Risk: Accidentally revoking user's only authentication method
  - Mitigation: Business rule validation in domain (min 1 active credential)

- Risk: Performance impact on large credential tables
  - Mitigation: Database index on (LastUsedAt, IsRevoked) columns

### Assumptions
- Assumption: LastUsedAt is reliably updated on every authentication
  - Validation: Verify in existing authentication handlers

- Assumption: 90 days is acceptable threshold
  - Validation: Confirm with product team (could be configurable)

### Open Questions
- Question: Should we send email notification before auto-revocation?
  - Owner: Product team (for now, no notification)

---

## Non-Functional Requirements

### Performance
- Requirement: Auto-revoke command completes within 5 seconds for 10,000 credentials

### Security
- Requirement: Only admin users can trigger manual revocation
- Requirement: Audit log all revocations

### Observability
- Logging: Log count of credentials revoked per run
- Metrics: Track credential_revocations_total counter
- Tracing: Trace auto-revoke command execution

---

## Implementation Notes

**Estimated Complexity**: Medium
**Estimated LOC**: ~300 lines (command + handler + tests)
**Estimated Duration**: 2-3 hours

**Implementation Approach**:
1. Start with domain layer: `AxonUserAuth.RevokeCredential()` method
2. Create `AutoRevokeInactiveCredentialsCommand` and handler
3. Add API endpoint with FastEndpoints
4. Generate comprehensive tests (4 unit + 2 integration + 1 E2E + 4 AC)
5. Verify 90%+ test coverage

**Layer Implementation Order** (bottom-up):
1. Domain: `AxonUserAuth` aggregate method + domain event
2. Application: Command, handler, validation
3. Infrastructure: Repository query for inactive credentials
4. API: FastEndpoints endpoint with authorization

---

## Dev Agent Record

**Context Reference**:
- Story Context: (Will be generated during execution)

**Implementation Log**:
- Started: (To be filled by workflow)
- Completed: (To be filled by workflow)
- Developer: AI-driven (Axon agents)

**Decision Log**:
- Decisions: (Will be captured in decisions.yaml)

**Documentation Updates**:
- (To be determined during implementation)

---

## Related Stories/Epics

**Epic**: PHASE-7-INTEGRATION-TESTS

**Related Stories**:
- TEST-002: Chat Message Ordering Validation
- TEST-003: FastEndpoint User Search
- TEST-004: Wallet Verification Refactoring
- TEST-005: Story Routing Bugfix

**Blocked By**: None
**Blocks**: None (independent test story)

---

## Change History

| Date | Change | Author |
|------|--------|--------|
| 2025-09-30 | Story created for Phase 7 integration testing | Valik + BMad Builder |