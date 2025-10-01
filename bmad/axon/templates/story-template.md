# Story: {{story_title}}

**Story ID**: {{story_id}}
**Status**: Draft | Approved | In Progress | Completed
**Module**: Identity | Chat | API | Cross-cutting
**Story Type**: Feature | Refactor | Bugfix
**Priority**: High | Medium | Low
**Complexity**: Simple | Medium | Complex
**Created**: {{created_date}}
**Last Updated**: {{last_updated}}

---

## File Location

This story will be saved as:
- `Docs/PROCESS/active-stories/{{story_id}}/story.md`

Workflow artifacts (3-file model):
- `implementation.log` - Living progress document with full timeline
- `decisions.yaml` - Structured learning log (created at completion)

---

## User Story

**As a** {{role}}
**I want** {{capability}}
**So that** {{business_value}}

---

## Technical Context

### Architecture Alignment

**Related Tech Spec**: {{tech_spec_path}} *(if from BMM planning)*

**Patterns Required**:
- Result<T, Error> error handling
- StrongId<T> for entity IDs
- CQRS (command/query separation)
- Domain events (if applicable)

**Module Boundaries**:
- Primary Module: {{module_name}}
- Dependencies: {{module_dependencies}}

**Relevant ADRs**:
- {{adr_id}}: {{adr_title}}

---

## Acceptance Criteria

**Testable Criteria** (one test per AC):

1. **AC1**: {{acceptance_criterion_1}}
   - Test: Given {{context}}, When {{action}}, Then {{expected_result}}

2. **AC2**: {{acceptance_criterion_2}}
   - Test: Given {{context}}, When {{action}}, Then {{expected_result}}

3. **AC3**: {{acceptance_criterion_3}}
   - Test: Given {{context}}, When {{action}}, Then {{expected_result}}

---

## Detailed Design

### Services & Components
{{#if tech_spec_reference}}
See Tech Spec: {{tech_spec_path}}#detailed-design
{{else}}
- Service/Class: {{service_name}}
  - Methods: {{methods}}
  - Dependencies: {{dependencies}}
{{/if}}

### Data Models
{{#if tech_spec_reference}}
See Tech Spec: {{tech_spec_path}}#data-models
{{else}}
- Entity: {{entity_name}}
  - Properties: {{properties}}
  - Relationships: {{relationships}}
{{/if}}

### APIs/Interfaces
{{#if tech_spec_reference}}
See Tech Spec: {{tech_spec_path}}#apis-interfaces
{{else}}
- Endpoint: {{method}} {{path}}
  - Request: {{request_model}}
  - Response: {{response_model}}
{{/if}}

---

## Dependencies & Integrations

**External Dependencies**:
- Library: {{library_name}} ({{purpose}})

**Module Dependencies**:
- Module: {{module_name}} ({{integration_point}})

**Database Changes**:
- Migration needed: {{yes_no}}
- New tables/columns: {{changes}}

---

## Test Strategy

### Unit Tests
- Test all new methods
- Test all domain logic
- Test error paths

### Integration Tests
- Test end-to-end workflows
- Test database integration
- Test external service integration

### AC Coverage Tests
- One test per acceptance criterion
- Clear naming: Given_When_Then format

---

## Risks & Assumptions

### Risks
- Risk: {{risk_description}}
  - Mitigation: {{mitigation_strategy}}

### Assumptions
- Assumption: {{assumption_description}}
  - Validation: {{how_to_validate}}

### Open Questions
- Question: {{question}}
  - Owner: {{who_will_answer}}

---

## Non-Functional Requirements

### Performance
- Requirement: {{performance_target}}

### Security
- Requirement: {{security_consideration}}

### Observability
- Logging: {{logging_requirements}}
- Metrics: {{metrics_to_track}}
- Tracing: {{tracing_requirements}}

---

## Implementation Notes

**Estimated Complexity**: {{simple_medium_complex}}
**Estimated LOC**: ~{{loc_estimate}}
**Estimated Duration**: {{time_estimate}}

**Implementation Approach**:
{{implementation_notes}}

---

## Dev Agent Record

**Context Reference**:
- Story Context: {{path_to_story_context_json}}

**Implementation Log**:
- Started: {{start_date}}
- Completed: {{completion_date}}
- Developer: AI-driven (Axon agents)

**Decision Log**:
- Decisions: {{path_to_decisions_yaml}}

**Documentation Updates**:
{{#docs_updated}}
- {{doc_path}} - {{section_updated}}
{{/docs_updated}}

---

## Related Stories/Epics

**Epic**: {{epic_id}} - {{epic_title}}

**Related Stories**:
- {{story_id}}: {{story_title}} ({{relationship}})

**Blocked By**: {{blocker_story_ids}}
**Blocks**: {{blocked_story_ids}}

---

## Change History

| Date | Change | Author |
|------|--------|--------|
| {{date}} | Story created | {{author}} |
| {{date}} | {{change_description}} | {{author}} |