# Story Template

## Story Metadata
- **Story ID**: [e.g., "1.2", "2.1"] 
- **Epic**: [Parent epic name]
- **Status**: Draft | Ready | In Progress | Review | Done
- **Assignee**: [Developer name]
- **Estimated Effort**: [S/M/L/XL]

## Story Description

### User Story
As a [user type], I want [goal] so that [benefit].

### Business Value
[Align with Axon AI Manifesto principles: "Ship at Lightspeed", "Conquer Complexity", "Safety is the Engine", etc.]

## Acceptance Criteria

- [ ] **Given** [context] **When** [action] **Then** [outcome]
- [ ] **Given** [context] **When** [action] **Then** [outcome]  
- [ ] **Given** [context] **When** [action] **Then** [outcome]

## Technical Requirements

### Architecture Compliance
- [ ] Follows Clean Architecture patterns
- [ ] Implements CQRS with MediatR
- [ ] Uses Result<T> pattern for error handling
- [ ] Strong ID types for all entities
- [ ] FluentValidation for input validation

### Research Gate ✅ MANDATORY
- [ ] **ADR Created**: Solution research completed with recommendation
- [ ] **Library Decision**: Build vs buy decision documented with evidence
- [ ] **Research Approval**: @axon-research-architect validation completed

## Implementation Notes

### Development Checklist
- [ ] Domain models created/updated
- [ ] Command/query handlers implemented  
- [ ] Validation rules added
- [ ] Repository implementations
- [ ] FastEndpoints endpoints
- [ ] Unit tests written (>90% coverage)
- [ ] Integration tests added

### Quality Checklist
- [ ] All acceptance criteria satisfied
- [ ] No compiler warnings
- [ ] All tests passing
- [ ] Code review completed
- [ ] Architecture compliance verified

## Testing Strategy

### Unit Tests
[Describe what needs unit test coverage]

### Integration Tests  
[Describe integration scenarios to test]

### Acceptance Tests
[Map to acceptance criteria]

## Definition of Done

- [ ] All acceptance criteria completed
- [ ] Code review approved  
- [ ] All tests passing
- [ ] Documentation updated
- [ ] Deployed to staging environment
- [ ] Product owner acceptance

## Dependencies

### Story Dependencies
- **Blocks**: [Stories that depend on this one]
- **Blocked by**: [Stories this one depends on]

### Epic Dependencies  
- **Epic Integration Points**: [How this story contributes to epic goals]

## Notes

### Development Notes
[Technical implementation notes, decisions made during development]

### QA Notes
[Quality assurance findings, issues found and resolved]