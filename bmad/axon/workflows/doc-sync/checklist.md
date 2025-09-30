# Doc-Sync - Validation Checklist

**Documentation synchronization validation**

Use this checklist to validate documentation drift detection and update generation.

---

## ✅ INITIALIZATION

- [ ] **Workflow Context Loaded**
  - [ ] workflow.yaml loaded and parsed
  - [ ] Inputs received from invoking workflow:
    - [ ] story_context (story ID, module context)
    - [ ] code_changes (list of changed file paths)
    - [ ] implementation_summary (what was implemented)
    - [ ] patterns_used (patterns applied)
  - [ ] Affected documentation layers identified (Identity/Chat/API)

---

## ✅ DRIFT DETECTION (@axon-doc-oracle)

### 4 Drift Types Checked
- [ ] **Missing Drift Checked**
  - [ ] Documentation doesn't exist for new code
  - [ ] New APIs without documentation
  - [ ] New domain concepts without explanation

- [ ] **Outdated Drift Checked**
  - [ ] Documentation exists but describes old behavior
  - [ ] Method signatures changed but docs unchanged
  - [ ] Business rules updated but docs stale

- [ ] **Incorrect Drift Checked**
  - [ ] Documentation contradicts actual code behavior
  - [ ] Examples don't match current implementation
  - [ ] ADR references outdated decisions

- [ ] **Orphaned Drift Checked**
  - [ ] Documentation exists for code that no longer exists
  - [ ] References to deleted methods/classes
  - [ ] Outdated examples no longer valid

### Drift Instances Identified
- [ ] **Each Drift Instance Documented**
  - [ ] Drift type assigned (Missing/Outdated/Incorrect/Orphaned)
  - [ ] Description provided
  - [ ] Code location referenced (file:line)
  - [ ] Doc location specified (file:section)
  - [ ] Severity assigned (Critical/High/Medium/Low)
  - [ ] Recommendation provided

### Affected Documentation Identified
- [ ] **Affected Docs Listed**
  - [ ] Identity module docs (if applicable)
  - [ ] Chat module docs (if applicable)
  - [ ] API docs (if applicable)
  - [ ] Sections affected specified
  - [ ] Update types identified (New/Modify/Delete)

### Output Validation
- [ ] **drift-detection.yaml Created**
  - [ ] File exists at correct path
  - [ ] YAML format valid
  - [ ] drift_summary section present
  - [ ] drifts_detected section present
  - [ ] affected_docs section present
  - [ ] overall_severity assigned

---

## ✅ DOC UPDATE GENERATION (@axon-doc-oracle)

### Updates Generated for Each Drift
- [ ] **For Each Drift Instance**
  - [ ] Before/after documentation generated
  - [ ] Reason for change provided
  - [ ] Code reference included (file:line)
  - [ ] Update type specified (New/Modify/Delete)

### Update Quality
- [ ] **Updates are Minimal**
  - [ ] Only changed sections addressed
  - [ ] No unnecessary rewrites
  - [ ] Focused on drift resolution

- [ ] **Updates are Targeted**
  - [ ] Specific sections identified
  - [ ] Clear before/after comparison
  - [ ] Actionable recommendations

- [ ] **Updates Reference Code**
  - [ ] file:line references provided
  - [ ] Code snippets included where helpful
  - [ ] Examples match actual implementation

### Documentation Update Template Used
- [ ] **Template Applied**
  - [ ] doc-update-template.md structure followed
  - [ ] Changes summary section present
  - [ ] Documentation updates section present
  - [ ] Inline XML updates section present
  - [ ] Validation checklist section present

### Output Validation
- [ ] **doc-updates.md Created**
  - [ ] File exists at correct path
  - [ ] Markdown format valid
  - [ ] All sections present
  - [ ] Updates for all drift instances
  - [ ] Inline XML updates included

---

## ✅ INLINE XML DOCUMENTATION (@axon-quality-guardian)

### Public API Documentation
- [ ] **Public Classes Documented**
  - [ ] <summary> tag present
  - [ ] Purpose described
  - [ ] Responsibilities listed
  - [ ] Usage examples provided (if complex)

- [ ] **Public Methods Documented**
  - [ ] <summary> tag present
  - [ ] <param> tags for each parameter
  - [ ] <returns> tag present
  - [ ] <exception> tags if applicable

- [ ] **Public Properties Documented**
  - [ ] <summary> tag present
  - [ ] What it represents described
  - [ ] Constraints documented (if any)

- [ ] **FastEndpoints Endpoints Documented**
  - [ ] Endpoint class has <summary>
  - [ ] Request DTO documented
  - [ ] Response DTO documented
  - [ ] Validator documented

### XML Comment Quality
- [ ] **XML Comments Follow Conventions**
  - [ ] Clear and concise descriptions
  - [ ] No redundant information (e.g., "Gets or sets X" for properties)
  - [ ] Business context provided (not just code mechanics)
  - [ ] Examples provided for complex APIs

### Coverage Validation
- [ ] **100% Coverage Achieved**
  - [ ] All public classes documented
  - [ ] All public methods documented
  - [ ] All public properties documented
  - [ ] All endpoints documented

---

## ✅ VALIDATION

### Drift Resolution Validation
- [ ] **All Drift Addressed**
  - [ ] drift_detection.yaml: Total count = [X]
  - [ ] doc-updates.md: Updates for [X] drift instances
  - [ ] 100% coverage (all drift has updates)

- [ ] **Zero Drift Achieved**
  - [ ] Either: No drift detected (count = 0)
  - [ ] Or: All drift has update recommendations

### Documentation Quality Validation
- [ ] **Updates are Complete**
  - [ ] All affected docs addressed
  - [ ] All sections updated
  - [ ] All inline XML added

- [ ] **Updates are Accurate**
  - [ ] Before/after correct
  - [ ] Code references accurate (file:line)
  - [ ] Examples match implementation

- [ ] **Updates are Minimal**
  - [ ] Only necessary changes
  - [ ] No over-documentation
  - [ ] Focused on drift resolution

---

## ✅ OUTPUT VALIDATION

- [ ] **2 Output Files Created**
  - [ ] drift-detection.yaml (valid YAML, all sections)
  - [ ] doc-updates.md (valid Markdown, all updates, template followed)

- [ ] **Files Accessible to Invoking Workflow**
  - [ ] File paths correct
  - [ ] Content parseable
  - [ ] Ready for application

---

## ✅ SUCCESS CRITERIA

- [ ] **Drift Detected**: All 4 types checked (Missing, Outdated, Incorrect, Orphaned)
- [ ] **Drift Count**: 0 OR all drift addressed in updates
- [ ] **Doc Updates Generated**: 100% (all drift has updates)
- [ ] **Inline XML Coverage**: 100% (all public APIs documented)
- [ ] **Execution Time**: ≤ 10 minutes

---

## ✅ RETURN TO INVOKING WORKFLOW

- [ ] **Control Returned**
  - [ ] drift-detection.yaml passed back
  - [ ] doc-updates.md passed back
  - [ ] Doc sync summary passed back
  - [ ] Zero drift status provided (Yes | No)

- [ ] **Invoking Workflow Ready to Apply Updates**
  - [ ] Documentation files to edit identified
  - [ ] Code files needing XML comments identified
  - [ ] Updates ready to apply before final commit

---

## 🎯 COMPLETION CRITERIA

**Doc-Sync Complete When**:
- ✅ All 4 drift types checked
- ✅ All drift instances identified and documented
- ✅ Updates generated for all drift (or zero drift)
- ✅ Inline XML coverage 100%
- ✅ drift-detection.yaml created (valid YAML)
- ✅ doc-updates.md created (valid Markdown)
- ✅ Zero drift status determined
- ✅ Control returned to invoking workflow

---

**Version:** 1.0.0  
**Last Updated:** 2025-09-30  
**Checklist Items:** 80+  
**Estimated Validation Time:** 2-3 minutes