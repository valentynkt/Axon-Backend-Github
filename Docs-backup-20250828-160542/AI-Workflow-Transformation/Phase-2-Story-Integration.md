# Phase 2: Story Workflow Integration

**Duration:** 20-30 minutes  
**Priority:** MEDIUM-HIGH  
**Dependencies:** Phase 1 complete  
**Focus:** Connect existing BMAD story commands with enhanced Axon agents

## Overview

This phase creates seamless integration between your existing sophisticated BMAD story workflow and your enhanced Axon agents. The key principle: **use what works, connect what's missing** - leverage your existing `/create-next-story`, `/shard-doc`, and quality gate commands with minimal integration code.

## Phase Objectives

1. **Connect Story Commands**: Link existing BMAD story creation to Axon agents
2. **Optimize Story Templates**: Use existing templates with minimal Axon-specific enhancements
3. **Create Handoff Protocols**: Smooth transitions between BMAD planning and Axon implementation
4. **Preserve BMAD Excellence**: Maintain all existing story workflow sophistication

## What We're NOT Doing

❌ **New Story Commands**: Your `/create-next-story` is already sophisticated  
❌ **Complex Templates**: Your existing story templates work well  
❌ **New Workflows**: Your brownfield workflows are already advanced  
❌ **BMAD Core Changes**: Zero modifications to working story system

## What We ARE Connecting

✅ **Story-to-Agent Handoffs**: Smooth transitions from BMAD stories to Axon implementation  
✅ **Context Templates**: Minimal story context for Axon agents  
✅ **Quality Integration**: Connect story quality gates with agents  
✅ **Brownfield Focus**: Enhance existing brownfield story patterns

---

## Step 1: Analyze Existing Story Workflow (5 minutes)

### Current Story System Assessment

**Review your existing story capabilities:**

```bash
# Check existing story commands
grep -r "create.*story\|story.*create" .bmad-core/ 2>/dev/null | head -5

# Check existing story location
ls -la docs/stories/ 2>/dev/null || echo "Story directory ready for use"

# Check existing brownfield commands
grep -r "brownfield" .bmad-core/ 2>/dev/null | head -3
```

**Document what's already working:**
- `/create-next-story` - Your sophisticated story creation
- `/brownfield-create-story` - Specialized for existing systems
- `/shard-doc` - Document sharding for development
- Story templates in BMAD core
- Quality gate integration

---

## Step 2: Create Story-Agent Connection Templates (15 minutes)

### Minimal Story Context Templates

**Create `.claude/contexts/story-context-template.md`:**

```markdown
# Story Context Template for Axon Agents

This template provides minimal context for Axon agents when working with BMAD-created stories.

## Story Information Structure

When a story is created with `/create-next-story` or `/brownfield-create-story`, it includes:

### Essential Story Elements
- **Story ID**: Unique identifier
- **Epic Context**: Parent epic information
- **Acceptance Criteria**: Testable requirements
- **Technical Requirements**: Architecture and implementation guidelines
- **Risk Assessment**: From BMAD risk profiling (if used)

### Axon Agent Context
- **Architecture Patterns**: Clean Architecture + CQRS + DDD
- **Technology Stack**: .NET 10, FastEndpoints, Entity Framework Core
- **Quality Standards**: Defined in coding standards
- **Business Alignment**: Axon AI manifesto principles

### Handoff Points

#### From BMAD Story Creation to Axon Research
```
BMAD: /create-next-story → Story created
→ @axon-research-architect for technical research
→ Research complete, ADR created
```

#### From Research to Implementation
```
Research: ADR approved
→ @axon-implementation-specialist for development
→ Implementation following story acceptance criteria
```

#### From Implementation to Quality
```
Implementation: Code complete
→ @axon-quality-guardian for business alignment validation
→ BMAD: /qa-gate for final quality decision
```

### Context Optimization
- Keep story context minimal (<500 words per story)
- Reference full story details in docs/stories/ as needed
- Use existing BMAD sharding for complex epics
```

### Story-Agent Integration Patterns

**Create `.claude/commands/story-agent-patterns.md`:**

```markdown
# Story-Agent Integration Patterns

Common workflows connecting BMAD story creation with Axon agent implementation.

## Pattern 1: Standard Story Development

### Workflow Steps
```bash
1. /create-next-story                    # BMAD story creation
2. @axon-research-architect             # Technical research (if needed)
3. @axon-implementation-specialist      # Code implementation
4. @axon-quality-guardian              # Business alignment check
5. /qa-gate                            # BMAD final quality validation
```

### Usage
- Use for standard feature stories
- Leverage existing BMAD story sophistication
- Add Axon technical implementation expertise

## Pattern 2: Brownfield Enhancement

### Workflow Steps  
```bash
1. /brownfield-create-story             # Specialized BMAD brownfield story
2. /advanced-elicitation               # Complex requirements for existing systems
3. @axon-research-architect           # Integration impact analysis
4. @axon-implementation-specialist    # Safe modification of existing system
5. Enhanced validation with existing system regression testing
6. /qa-gate                          # Final validation
```

### Usage
- Your specialization: existing system enhancement
- Leverages sophisticated BMAD brownfield commands
- Adds Axon technical precision for existing systems

## Pattern 3: High-Risk Story Development

### Workflow Steps
```bash
1. /create-next-story                  # Initial story creation
2. *risk                              # BMAD QA Test Architect risk assessment
3. @axon-research-architect          # Enhanced research for high-risk areas
4. *design                           # Test design automation
5. @axon-implementation-specialist   # Implementation with enhanced validation
6. *trace + @axon-quality-guardian   # Requirements traceability + business validation
7. /qa-gate                         # Final gate with risk consideration
```

### Usage
- For stories with high technical or business risk
- Combines BMAD risk assessment with Axon technical expertise
- Uses existing sophisticated QA Test Architect capabilities

## Pattern 4: Epic-Level Coordination

### Workflow Steps
```bash
1. /brownfield-create-epic            # Epic planning with BMAD
2. /shard-doc                        # Document sharding for development
3. @axon-story-orchestrator         # Coordinate multiple related stories
4. Parallel story development using Patterns 1-3
5. Epic integration validation
6. Stakeholder delivery
```

### Usage
- Large initiatives requiring multiple stories
- Leverages BMAD document sharding excellence  
- Adds Axon epic-level coordination capabilities

## Integration Notes

### BMAD Strengths to Preserve
- Sophisticated story creation templates
- Advanced brownfield specialization
- Comprehensive QA Test Architect system
- Document sharding and epic management

### Axon Agent Value-Add
- Technical implementation expertise
- Architecture pattern enforcement
- Business alignment validation
- Clean code and testing standards

### Handoff Optimization
- Minimal context transfer between phases
- Use existing BMAD outputs as Axon inputs
- Preserve all existing sophisticated capabilities
```

---

## Step 3: Create Story Development Context (5 minutes)

### Minimal Story Development Context

**Create `.claude/contexts/axon-story-development.md`:**

```markdown
# Axon Story Development Context

Minimal context for Axon agents working with BMAD-created stories.

## Development Standards

### Architecture Requirements (from coding-standards.md)
- Clean Architecture: Domain → Application → Infrastructure → API
- CQRS with MediatR for command/query separation  
- Result<T> pattern for functional error handling
- Strong IDs for type-safe entity identification

### Technology Stack (from tech-stack.md)
- .NET 10 with modern C# features
- FastEndpoints for API endpoints
- Entity Framework Core 9.0 with PostgreSQL
- FluentValidation for request validation

### Story Integration Points

#### With BMAD Story Creation
- Stories created via `/create-next-story` or `/brownfield-create-story`
- Use existing story templates and acceptance criteria
- Leverage BMAD risk assessment when available

#### With BMAD Quality Gates
- Integration with `/qa-gate` for final validation
- Use `*trace` for requirements traceability
- Connect with `*review` for code review integration

#### Business Alignment
- Axon AI manifesto principle advancement
- Focus on existing system enhancement (brownfield specialization)
- Safety-first architecture and non-custodial patterns

### File Locations
- Stories: `docs/stories/` 
- Architecture docs: `docs/architecture/`
- QA outputs: `docs/qa/` (configured in Phase 0)

Keep this context minimal - detailed information available in referenced docs.
```

### Story Handoff Validation

**Create simple validation script:**

```bash
# Create validation helper
cat > docs/AI-Workflow-Transformation/validate-story-integration.sh << 'EOF'
#!/bin/bash

echo "=== Story Integration Validation ==="

# Check story context templates
if [ -f ".claude/contexts/story-context-template.md" ]; then
    echo "✅ Story context template created"
else
    echo "❌ Story context template missing"
fi

# Check integration patterns
if [ -f ".claude/commands/story-agent-patterns.md" ]; then
    echo "✅ Story-agent patterns created"
    patterns=$(grep -c "## Pattern" ".claude/commands/story-agent-patterns.md")
    echo "   $patterns integration patterns available"
else
    echo "❌ Story-agent patterns missing"
fi

# Check development context
if [ -f ".claude/contexts/axon-story-development.md" ]; then
    echo "✅ Development context created"
else
    echo "❌ Development context missing"
fi

# Verify BMAD story commands still work
echo "=== BMAD Story Command Check ==="
if grep -r "create.*story" .bmad-core/ >/dev/null 2>&1; then
    echo "✅ BMAD story commands preserved"
else
    echo "⚠️  BMAD story commands not found (may be expected)"
fi

# Check docs/stories directory
if [ -d "docs/stories" ]; then
    story_count=$(ls docs/stories/*.md 2>/dev/null | wc -l)
    echo "✅ Stories directory ready ($story_count existing stories)"
else
    echo "⚠️  Stories directory not found"
fi

echo "=== Integration validation complete ==="
EOF

chmod +x docs/AI-Workflow-Transformation/validate-story-integration.sh
```

---

## Step 4: Test Story-Agent Integration (5 minutes)

### Integration Testing

**Test the connection between BMAD and Axon:**

```bash
# Run validation
./docs/AI-Workflow-Transformation/validate-story-integration.sh

# Test context file sizes (ensure they remain minimal)
echo "=== Context Size Validation ==="
for file in .claude/contexts/*.md; do
    if [ -f "$file" ]; then
        words=$(wc -w < "$file")
        echo "$(basename "$file"): $words words"
        if [ $words -gt 200 ]; then
            echo "   ⚠️  Consider reducing size for optimal performance"
        fi
    fi
done
```

### Success Validation Checklist

- [ ] Story context template created (minimal, <500 words)
- [ ] Story-agent patterns documented (4 main patterns)
- [ ] Development context created (architecture standards reference)
- [ ] Integration validated with existing BMAD commands
- [ ] All context files remain performant (<200 words each)
- [ ] Zero changes to existing BMAD story system

---

## Expected Outcomes

### Story Workflow Enhancement
- **Seamless Handoffs**: Smooth transitions from BMAD story creation to Axon implementation
- **Preserved Sophistication**: All existing BMAD story capabilities maintained
- **Enhanced Quality**: Better technical implementation with business alignment
- **Brownfield Focus**: Optimized patterns for existing system enhancement

### Performance Maintained  
- **Minimal Context**: All new context files <200 words each
- **Fast Integration**: Quick handoffs between BMAD and Axon phases
- **No BMAD Changes**: Zero modifications to working story system

### Foundation for Quality Phase
- **Quality Integration Points**: Ready for advanced QA Test Architect integration
- **Story-Quality Connection**: Stories connected to quality validation workflow
- **Risk Integration**: Story risk assessment connected to quality gates

---

## Troubleshooting

### Common Issues

| Issue | Symptom | Solution |
|-------|---------|----------|
| **Context Too Large** | Slow agent performance | Reduce context files to <200 words |
| **Poor Handoffs** | Agents missing story context | Review story context template |
| **BMAD Integration Lost** | Story commands not working | Verify no changes made to BMAD core |
| **Missing Patterns** | Unclear workflow transitions | Review story-agent patterns |

### Quick Fixes

```bash
# Check context sizes
find .claude/contexts -name "*.md" -exec wc -w {} \;

# Verify BMAD preservation
ls -la .bmad-core/workflows/ .bmad-core/templates/

# Test story directory
ls -la docs/stories/
```

---

## Phase Completion Deliverables

### 1. Story-Agent Connection System
- Story context template for minimal agent context
- Story-agent integration patterns (4 main workflows)
- Development context with architecture standards

### 2. Integration Validation
- Validation script for testing connections
- Context size optimization (<200 words per file)
- BMAD system preservation verification

### 3. Workflow Patterns
- Standard story development workflow
- Brownfield enhancement specialization
- High-risk story development process
- Epic-level coordination patterns

---

## Next Steps

### Phase 2 Success Criteria Met ✅
- Story workflow seamlessly integrated with enhanced Axon agents
- Existing BMAD story sophistication fully preserved
- Minimal context templates created for optimal performance
- Four integration patterns documented for common scenarios

**→ Continue to [Phase 3: Testing & Quality Integration](Phase-3-Testing-Quality.md)**

### Key Handoffs to Phase 3
1. **Story-Quality Connection**: Stories now connected to quality validation
2. **Risk Integration Ready**: Story risk assessment ready for QA Test Architect
3. **Context Optimization**: Minimal context ready for quality enhancement
4. **BMAD Preservation**: All sophisticated QA capabilities preserved and ready

**Phase 2 Complete → Ready for Advanced QA Test Architect Integration**