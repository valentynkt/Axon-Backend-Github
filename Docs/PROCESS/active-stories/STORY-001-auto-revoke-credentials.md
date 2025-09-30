# Story: Add Auto-Revoke Old Credentials

**Story ID**: STORY-001
**Status**: In Progress (Integration Test)
**Module**: Identity
**Story Type**: Feature
**Priority**: High
**Complexity**: Medium
**Created**: 2025-09-30
**Last Updated**: 2025-09-30

---

## User Story

**As a** Security Administrator
**I want** credentials older than 90 days to be automatically revoked
**So that** we maintain security hygiene and reduce attack surface from stale credentials

---

## Technical Context

### Architecture Alignment

**Patterns Required**:
- Result<T, Error> error handling
- StrongId<T> for entity IDs (AxonUserId)
- CQRS (command for revocation logic)
- Domain events (CredentialRevokedEvent)

**Module Boundaries**:
- Primary Module: Identity
- Dependencies: None (self-contained within Identity module)

**Relevant ADRs**:
- ADR-002: CQRS Pattern
- ADR-003: Result Pattern
- ADR-005: PostgreSQL with xmin concurrency

---

## Acceptance Criteria

**Testable Criteria** (one test per AC):

1. **AC1**: Credentials older than 90 days are identified correctly
   - Test: Given a principal with credentials from 91 days ago, When auto-revocation runs, Then credentials are marked for revocation

2. **AC2**: Revoked credentials cannot be used for authentication
   - Test: Given a revoked credential, When attempting to exchange/authenticate, Then authentication fails with appropriate error

3. **AC3**: Recently used credentials (< 90 days) are never revoked
   - Test: Given credentials used 89 days ago, When auto-revocation runs, Then credentials remain active

4. **AC4**: Auto-revocation is idempotent
   - Test: Given already-revoked credentials, When auto-revocation runs again, Then no errors occur and state remains consistent

5. **AC5**: Domain event is raised when credentials are revoked
   - Test: Given credentials being revoked, When revocation executes, Then CredentialRevokedEvent is raised with correct data

---

## Detailed Design

### Services & Components

**New Command**:
- `RevokeStaleCredentialsCommand` (Application layer)
  - Handler: `RevokeStaleCredentialsCommandHandler`
  - Base: `BaseIdentityCommandHandler`

**Domain Changes**:
- `AxonPrincipal.Commands.cs` - Add `RevokeStaleCredentials(TimeSpan maxAge)` method
- New domain event: `CredentialRevokedEvent`

**Infrastructure**:
- Background job/scheduler (optional - can be manual trigger for MVP)
- Query to find principals with stale credentials

### Data Models

**Existing Entity Modified**:
- `IdentityCredential` (owned entity)
  - Add property: `RevokedAt` (DateTime?)
  - Add property: `RevocationReason` (string?)
  - Update `LastSeen` tracking logic

**Domain Event**:
- `CredentialRevokedEvent`
  - Properties: PrincipalId, CredentialId, RevokedAt, Reason

### APIs/Interfaces

**New Admin Endpoint** (optional for MVP):
- Endpoint: POST /api/v1/admin/auth/revoke-stale-credentials
  - Request: `{ "maxAgeDays": 90 }`
  - Response: `{ "revokedCount": number, "affectedPrincipals": number }`

---

## Dependencies & Integrations

**External Dependencies**:
- None (uses existing Identity infrastructure)

**Module Dependencies**:
- None (self-contained)

**Database Changes**:
- Migration needed: Yes
- New columns: `IdentityCredential.RevokedAt`, `IdentityCredential.RevocationReason`

---

## Test Strategy

### Unit Tests (Domain Layer)
- `AxonPrincipal.RevokeStaleCredentials()` behavior
- Edge cases: empty credentials, all stale, all fresh, mixed
- Invariant validation: credential uniqueness preserved
- Domain event emission

### Integration Tests (Application Layer)
- `RevokeStaleCredentialsCommandHandler` end-to-end
- Database persistence of revocation state
- Idempotency validation
- Query performance for large datasets

### AC Coverage Tests
- One test per AC (5 tests)
- Clear naming: `Given_CredentialsOlderThan90Days_When_AutoRevocationRuns_Then_CredentialsRevoked`

### Infrastructure Tests
- EF Core configuration for new columns
- Migration up/down testing
- Composite key integrity maintained

---

## Risks & Assumptions

### Risks
- Risk: Revoking actively-used credentials
  - Mitigation: Check `LastSeen` timestamp, require both age AND inactivity

- Risk: Performance impact on large credential sets
  - Mitigation: Batch processing, add indexes on RevokedAt/LastSeen

### Assumptions
- Assumption: `LastSeen` is reliably updated on each credential use
  - Validation: Review ExchangeCredentialHandler code

- Assumption: 90 days is the correct threshold
  - Validation: Confirm with product team

### Open Questions
- Question: Should revocation be hard delete or soft delete?
  - Owner: Security team / Product
  - **Decision**: Soft delete (keep audit trail)

---

## Non-Functional Requirements

### Performance
- Requirement: Revocation query executes in < 1 second for 100k credentials

### Security
- Requirement: Only admins can trigger manual revocation
- Requirement: Revocation is logged for audit trail

### Observability
- Logging: Log each revocation with PrincipalId, CredentialId, Reason
- Metrics: Track revocation count, affected principals count
- Tracing: Trace through entire revocation workflow

---

## Implementation Notes

**Estimated Complexity**: Medium
**Estimated LOC**: ~200-300 lines
**Estimated Duration**: 3-4 hours

**Implementation Approach**:
1. **Domain First**: Add `RevokeStaleCredentials()` to AxonPrincipal.Commands.cs
2. **Events**: Add CredentialRevokedEvent
3. **Application**: Create command + handler
4. **Infrastructure**: EF Core migration for new columns
5. **Tests**: Domain → Application → Infrastructure → E2E
6. **API**: Optional admin endpoint (can defer)

**Key Pattern to Follow**:
- Look at existing `AxonPrincipal` command methods for pattern
- Follow owned entity mutation patterns (maintain composite keys)
- Ensure single xmin concurrency token (aggregate root level)

---

## Dev Agent Record

**Context Reference**:
- Story Context: TBD (generated during workflow execution)

**Implementation Log**:
- Started: 2025-09-30
- Completed: TBD
- Developer: AI-driven (Axon agents - Integration Test)

**Decision Log**:
- Decisions: TBD (generated during workflow)

**Documentation Updates**:
- TBD (to be captured during implementation)

---

## Related Stories/Epics

**Epic**: EPIC-001 - Identity Security Enhancements

**Related Stories**:
- STORY-002: Add credential usage analytics (enhancement)

**Blocked By**: None
**Blocks**: None

---

## Change History

| Date | Change | Author |
|------|--------|--------|
| 2025-09-30 | Story created for integration testing | BMad Builder + Valik |