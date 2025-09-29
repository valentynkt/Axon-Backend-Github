# Load Documentation Context Task

**Agent**: Axon Doc Oracle
**Purpose**: Load documentation using hub-and-spoke progressive disclosure strategy

---

## Task Instructions

### Hub-and-Spoke Strategy

**Core Hub (ALWAYS loaded)**:
```yaml
core_hub:
  - Docs/ENGINEERING/00-START-HERE.md
  - Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md  # 80% of patterns
  - Docs/ENGINEERING/guides/architecture/system-overview.md
  - Docs/Libraries/00-INDEX.md
  - Docs/ENGINEERING/guides/architecture/adrs/00-INDEX.md   # ADR catalog
```

**Module Spokes (context-dependent)**:
```yaml
identity_spoke:
  - Docs/ENGINEERING/modules/identity/00-INDEX.md
  - Docs/ENGINEERING/modules/identity/01-domain-model.md
  - Docs/ENGINEERING/modules/identity/03-authentication.md
  - Docs/ENGINEERING/modules/identity/05-api-contracts.md
  - Docs/ENGINEERING/modules/identity/06-database-schema.md

chat_spoke:
  - Docs/ENGINEERING/modules/chat/00-INDEX.md
  - Docs/ENGINEERING/modules/chat/01-domain-model.md
  - Docs/ENGINEERING/modules/chat/03-messaging-flows.md
  - Docs/ENGINEERING/modules/chat/05-api-contracts.md
  - Docs/ENGINEERING/modules/chat/06-database-schema.md
```

**Pattern Spokes (need-based)**:
```yaml
pattern_spokes:
  - Docs/ENGINEERING/guides/patterns/cqrs.md
  - Docs/ENGINEERING/guides/patterns/domain-modeling.md
  # Note: error-handling and validation are in QUICK-REFERENCE
```

**ADR Spokes (reference-based)**:
```yaml
adr_spokes:
  - Docs/ENGINEERING/guides/architecture/adrs/001-modular-monolith.md
  - Docs/ENGINEERING/guides/architecture/adrs/002-cqrs-mediatr.md
  - Docs/ENGINEERING/guides/architecture/adrs/003-result-pattern.md
  - Docs/ENGINEERING/guides/architecture/adrs/004-strong-ids.md
  - Docs/ENGINEERING/guides/architecture/adrs/005-postgresql.md
  - Docs/ENGINEERING/guides/architecture/adrs/006-fastendpoints.md
```

**Library Spokes (on-demand)**:
```yaml
library_spokes:
  core:
    - Docs/Libraries/MediatR/IMPLEMENTATION_GUIDE.md
    - Docs/Libraries/FastEndpoints/IMPLEMENTATION_GUIDE.md
    - Docs/Libraries/FluentValidation/IMPLEMENTATION_GUIDE.md
  # 15+ library guides available in Libraries/ directory
```

### Process

1. **Load Core Hub** (always)
2. **Detect Context** from story:
   - Module context → Load module spoke
   - Pattern mentions → Load pattern spoke
   - Library usage → Load library spoke
   - ADR references → Load specific ADRs
3. **Expand On-Demand** during workflow execution

### Output
- List of loaded documents
- Context summary
- Available spokes for expansion

---

## TODO: Full Implementation
Implement progressive loading with Read tool and context tracking.