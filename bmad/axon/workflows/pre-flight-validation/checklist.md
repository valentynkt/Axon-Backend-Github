# Pre-Flight Validation - Validation Checklist

**Reusable support workflow validation**

Use this checklist to validate pre-flight validation execution across all invoking workflows (story-implementation, story-refactoring, module workflows).

---

## ✅ INITIALIZATION

- [ ] **Workflow Context Loaded**
  - [ ] workflow.yaml loaded and parsed
  - [ ] Inputs received from invoking workflow:
    - [ ] story_context (metadata, ACs, tech spec)
    - [ ] module_context (Identity | Chat | API | Cross-cutting)
    - [ ] docs_loaded (list of docs already loaded)
    - [ ] patterns_required (list of required patterns)
  - [ ] Output paths set for 3 reports

---

## ✅ PARALLEL EXECUTION

- [ ] **All 3 Agents Launched Concurrently**
  - [ ] @axon-archaeologist launched (discovery)
  - [ ] @axon-library-sage launched (library validation)
  - [ ] @axon-doc-oracle launched (pattern compliance)
  - [ ] Parallel execution confirmed (not sequential)

---

## ✅ DISCOVERY REPORT (@axon-archaeologist)

### 5-Layer Search Complete
- [ ] **Domain Layer Searched**
  - [ ] Aggregates discovered
  - [ ] Entities discovered
  - [ ] Value objects discovered
  - [ ] Domain events discovered
  - [ ] Specifications discovered

- [ ] **Application Layer Searched**
  - [ ] Commands discovered
  - [ ] Queries discovered
  - [ ] Handlers discovered
  - [ ] Services discovered
  - [ ] DTOs discovered

- [ ] **Infrastructure Layer Searched**
  - [ ] Repositories discovered
  - [ ] EF Core configurations discovered
  - [ ] External service clients discovered

- [ ] **API Layer Searched**
  - [ ] Endpoints discovered
  - [ ] Validators discovered
  - [ ] Contracts discovered

- [ ] **Cross-Module Layer Searched**
  - [ ] Shared kernel components discovered
  - [ ] Building blocks discovered

### Reuse Recommendations
- [ ] **REUSE Components Identified**
  - [ ] Components that can be used as-is listed
  - [ ] Locations provided (file:line)

- [ ] **EXTEND Components Identified**
  - [ ] Components that can be extended listed
  - [ ] Extension approach described

- [ ] **ADAPT Patterns Identified**
  - [ ] Patterns that can be adapted listed
  - [ ] Adaptation needed described

- [ ] **CREATE Components Identified**
  - [ ] New components needed listed
  - [ ] Justification provided

### Reuse Score Calculated
- [ ] **Reuse Score Assigned**
  - [ ] High: 70%+ reuse
  - [ ] Medium: 30-70% reuse
  - [ ] Low: <30% reuse

### Output Validation
- [ ] **discovery-report.yaml Created**
  - [ ] File exists at correct path
  - [ ] YAML format valid
  - [ ] All sections present (REUSE, EXTEND, ADAPT, CREATE)
  - [ ] Reuse score present
  - [ ] Similar patterns listed with locations

---

## ✅ LIBRARY VALIDATION (@axon-library-sage)

### 4 Library Categories Checked
- [ ] **Core Libraries Checked**
  - [ ] MediatR capabilities assessed
  - [ ] FluentValidation capabilities assessed
  - [ ] FastEndpoints capabilities assessed

- [ ] **Data Libraries Checked**
  - [ ] EF Core capabilities assessed
  - [ ] Dapper capabilities assessed (if applicable)

- [ ] **External Libraries Checked**
  - [ ] Dynamic.xyz capabilities assessed (if applicable)
  - [ ] OpenAI capabilities assessed (if applicable)
  - [ ] Helius capabilities assessed (if applicable)

- [ ] **Infrastructure Libraries Checked**
  - [ ] OpenTelemetry capabilities assessed (if applicable)
  - [ ] Polly capabilities assessed (if applicable)
  - [ ] Serilog capabilities assessed (if applicable)

### 4-Factor Scoring Complete
- [ ] **Capability Match Assessed**
  - [ ] How well libraries cover requirements (0-100%)

- [ ] **Complexity Reduction Assessed**
  - [ ] How much manual code avoided

- [ ] **Maintenance Burden Assessed**
  - [ ] Ongoing maintenance cost evaluated

- [ ] **Integration Cost Assessed**
  - [ ] Effort to integrate evaluated

### Recommendations Generated
- [ ] **Library Recommendations Provided**
  - [ ] Use: Libraries to use as-is
  - [ ] Extend: Libraries to extend
  - [ ] Manual: What must be written manually

- [ ] **Manual Code Identified**
  - [ ] Components that need manual code
  - [ ] Reasons why no library covers them
  - [ ] Complexity estimated (Low | Medium | High)

### Output Validation
- [ ] **library-validation.yaml Created**
  - [ ] File exists at correct path
  - [ ] YAML format valid
  - [ ] Library recommendations present
  - [ ] 4-factor scoring complete
  - [ ] Manual code needs identified
  - [ ] Integration complexity assessed

---

## ✅ PATTERN COMPLIANCE (@axon-doc-oracle)

### 5 Pattern Dimensions Validated
- [ ] **Result<T, Error> Pattern**
  - [ ] All operations return Result<T>
  - [ ] No exceptions thrown in domain/application
  - [ ] Error types appropriate
  - [ ] Compliance score: [0-100%]

- [ ] **StrongId<T> Pattern**
  - [ ] All IDs are StrongId<T>
  - [ ] No primitive obsession (Guid, int)
  - [ ] Vogen value objects used
  - [ ] Compliance score: [0-100%]

- [ ] **CQRS Pattern**
  - [ ] Commands separated from queries
  - [ ] MediatR handlers implemented
  - [ ] Request/response patterns correct
  - [ ] Compliance score: [0-100%]

- [ ] **Domain Events Pattern**
  - [ ] Events raised after mutations
  - [ ] Event handlers implemented
  - [ ] Event naming conventions followed
  - [ ] Compliance score: [0-100%]

- [ ] **Owned Entities Pattern** (if applicable)
  - [ ] EF Core OwnsMany used
  - [ ] Composite keys configured
  - [ ] No independent DbSet
  - [ ] Compliance score: [0-100%]

### ADR Validation (6 ADRs)
- [ ] **ADR-001: Modular Monolith**
  - [ ] Applies to story: Yes | No
  - [ ] Compliance: [0-100%]

- [ ] **ADR-002: CQRS with MediatR**
  - [ ] Applies to story: Yes | No
  - [ ] Compliance: [0-100%]

- [ ] **ADR-003: Result Pattern**
  - [ ] Applies to story: Yes | No
  - [ ] Compliance: [0-100%]

- [ ] **ADR-004: Strong IDs**
  - [ ] Applies to story: Yes | No
  - [ ] Compliance: [0-100%]

- [ ] **ADR-005: PostgreSQL + xmin**
  - [ ] Applies to story: Yes | No
  - [ ] Compliance: [0-100%]

- [ ] **ADR-006: FastEndpoints**
  - [ ] Applies to story: Yes | No
  - [ ] Compliance: [0-100%]

### Violations Identified
- [ ] **Pattern Violations Listed**
  - [ ] Each violation described
  - [ ] Location provided (file:line)
  - [ ] Recommendation provided (how to fix)

### Output Validation
- [ ] **pattern-compliance.yaml Created**
  - [ ] File exists at correct path
  - [ ] YAML format valid
  - [ ] Overall compliance score present
  - [ ] Pattern breakdown present (5 dimensions)
  - [ ] Violations listed with recommendations
  - [ ] ADR alignment present

---

## ✅ CONSOLIDATION

- [ ] **All 3 Reports Complete**
  - [ ] discovery-report.yaml exists
  - [ ] library-validation.yaml exists
  - [ ] pattern-compliance.yaml exists

- [ ] **Consolidated Summary Generated**
  - [ ] Discovery summary (reuse score, counts)
  - [ ] Library summary (coverage, manual code %)
  - [ ] Pattern summary (compliance score, violations)
  - [ ] Overall assessment (Ready | Review | Block)
  - [ ] Blockers identified (if any)
  - [ ] Key recommendations listed

---

## ✅ SUCCESS CRITERIA

- [ ] **Discovery Complete**: 100% (all 5 layers searched)
- [ ] **Reuse Score Calculated**: High | Medium | Low
- [ ] **Library Validation Complete**: 100% (all categories checked)
- [ ] **Pattern Compliance Score**: ≥ 95%
- [ ] **Execution Time**: ≤ 15 minutes
- [ ] **Parallel Execution**: All 3 agents ran concurrently

---

## ✅ OUTPUT VALIDATION

- [ ] **3 YAML Reports Created**
  - [ ] discovery-report.yaml (valid YAML, all sections)
  - [ ] library-validation.yaml (valid YAML, recommendations, scoring)
  - [ ] pattern-compliance.yaml (valid YAML, compliance scores, ADRs)

- [ ] **Reports Accessible to Invoking Workflow**
  - [ ] File paths correct
  - [ ] Content parseable
  - [ ] Ready for Checkpoint 2 presentation

---

## ✅ RETURN TO INVOKING WORKFLOW

- [ ] **Control Returned**
  - [ ] All 3 reports passed back
  - [ ] Consolidated summary passed back
  - [ ] Approval recommendation provided (Proceed | Review | Block)

- [ ] **Checkpoint 2 Ready**
  - [ ] User can review all 3 reports
  - [ ] Summary provides clear decision point
  - [ ] Blockers clearly identified (if any)

---

## 🎯 COMPLETION CRITERIA

**Pre-Flight Validation Complete When**:
- ✅ All 3 agents executed in parallel
- ✅ Discovery complete (5 layers, reuse score)
- ✅ Library validation complete (4 categories, recommendations)
- ✅ Pattern compliance validated (5 dimensions, ADRs)
- ✅ All 3 reports created (valid YAML)
- ✅ Consolidated summary generated
- ✅ Execution time ≤ 15 minutes
- ✅ Control returned to invoking workflow

---

**Version:** 1.0.0  
**Last Updated:** 2025-09-30  
**Checklist Items:** 100+  
**Estimated Validation Time:** 2-3 minutes