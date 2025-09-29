# Axon Backend Documentation

**Three-domain documentation architecture: PRODUCT | ENGINEERING | PROCESS**

---

## 🎯 Quick Navigation

### I'm looking for... → Go to:

| What You Need | Documentation Domain |
|---------------|---------------------|
| **Business vision, strategy, MVP scope** | [PRODUCT/](./PRODUCT/00-INDEX.md) |
| **Technical architecture, code patterns, APIs** | [ENGINEERING/](./ENGINEERING/00-START-HERE.md) |
| **Project workflow, stories, sprint artifacts** | [PROCESS/](./PROCESS/00-INDEX.md) |

---

## 📚 The Three Documentation Domains

### 1. PRODUCT/ - Business & Strategy
**WHO**: Leadership, product managers, investors, business stakeholders
**WHAT**: WHY we build what we build
**START HERE**: [`PRODUCT/00-INDEX.md`](./PRODUCT/00-INDEX.md)

**Contains**:
- Vision & manifesto
- Business requirements document (BRD)
- MVP feature blueprint
- Ideal customer profile (ICP)
- Value propositions (B2B & B2C)
- Market positioning & GTM strategy

---

### 2. ENGINEERING/ - Technical Implementation
**WHO**: Developers, architects, QA engineers
**WHAT**: HOW we build the system
**START HERE**: [`ENGINEERING/00-START-HERE.md`](./ENGINEERING/00-START-HERE.md)

**Contains**:
- System architecture & design patterns
- Module documentation (Identity, Chat)
- Shared patterns (BuildingBlocks)
- Infrastructure guides (persistence, caching, observability)
- Testing strategies
- Development & deployment guides
- API documentation
- AI-optimized navigation

---

### 3. PROCESS/ - Workflow & Project Management
**WHO**: Project managers, scrum masters, team leads
**WHAT**: WHEN/WHO builds features
**START HERE**: [`PROCESS/00-INDEX.md`](./PROCESS/00-INDEX.md)

**Contains**:
- BMAD methodology
- Story templates
- Active user stories
- Archived epics & QA gates
- Sprint planning artifacts

---

## 🏗️ Documentation Principles

### Single Source of Truth
Each concept is documented **once**, in the correct domain. No duplication.

### Progressive Disclosure
- **Overview files** (00-XXX-OVERVIEW.md) provide navigation and high-level context
- **Quick reference** files provide 80/20 rule hot paths
- **Detailed docs** provide deep-dive implementation guidance

### Clear Ownership
- **PRODUCT** = Product team owns
- **ENGINEERING** = Engineering team owns
- **PROCESS** = PM/Scrum team owns

### AI-Optimized
- Logical structure for context loading
- Clear entry points per domain
- Task-to-document mapping in `ENGINEERING/ai-context/`

---

## 📊 Documentation Statistics

```
Total Domains: 3 (PRODUCT, ENGINEERING, PROCESS)
Total Files: ~120 markdown files (excluding Libraries/)
Documentation Size: ~600KB
Duplication: 0% (zero overlap between domains)
Clarity Score: 9/10
```

---

## 🚀 For New Team Members

### Onboarding Path

**Day 1**: Business Context
1. Read [PRODUCT/vision-manifesto.md](./PRODUCT/vision-manifesto.md)
2. Read [PRODUCT/business-requirements.md](./PRODUCT/business-requirements.md)
3. Read [PRODUCT/mvp-feature-blueprint.md](./PRODUCT/mvp-feature-blueprint.md)

**Day 2**: Technical Context
1. Read [ENGINEERING/00-START-HERE.md](./ENGINEERING/00-START-HERE.md)
2. Read [ENGINEERING/architecture/00-QUICK-REFERENCE.md](./ENGINEERING/architecture/00-QUICK-REFERENCE.md)
3. Read [ENGINEERING/architecture/system-overview.md](./ENGINEERING/architecture/system-overview.md)

**Day 3**: Module Deep-Dive
1. Read [ENGINEERING/modules/identity/](./ENGINEERING/modules/identity/00-INDEX.md)
2. Read [ENGINEERING/modules/chat/](./ENGINEERING/modules/chat/00-INDEX.md)
3. Set up local environment using [ENGINEERING/guides/](./ENGINEERING/guides/00-INDEX.md)

---

## 🔗 External References

- **Source Code**: `/src` directory (modular monolith structure)
- **Tests**: `/tests` directory (mirrors `/src` structure)
- **Library Docs**: `/Docs/Libraries/` (20+ implementation guides)
- **CI/CD**: `.github/workflows/`

---

## 📝 Contributing to Documentation

### When to Update Which Domain

**Update PRODUCT/** when:
- Business strategy changes
- New value propositions identified
- Market positioning shifts
- MVP scope adjustments

**Update ENGINEERING/** when:
- New architectural patterns added
- Module documentation needed
- Infrastructure changes
- API contract updates

**Update PROCESS/** when:
- Workflow methodology changes
- New story templates needed
- Sprint process updates

### Documentation Standards

- Use mocked file format for scaffolds: `## Content to be filled:` with bullet points
- Add metadata headers: `**STATUS**: 🚧/✅`, `**PRIORITY**: High/Med/Low`
- Keep hub files (00-XXX) as navigation only, not content
- Link related docs across domains
- Update `**LAST_UPDATED**` date when changing

---

## 🤖 For AI Agents

### Context Loading Strategy

1. **Identify domain**: Product (WHY) vs Engineering (HOW) vs Process (WHEN)
2. **Load entry point**: Relevant `00-XXX` file for domain
3. **Load specific doc**: Based on task type
4. **Load dependencies**: Related cross-domain docs as needed

### Quick Reference

For 80% of technical queries, load:
- [`ENGINEERING/architecture/00-QUICK-REFERENCE.md`](./ENGINEERING/architecture/00-QUICK-REFERENCE.md)

For task routing, load:
- [`ENGINEERING/ai-context/quick-reference-index.md`](./ENGINEERING/ai-context/quick-reference-index.md)

---

**Last Major Restructure**: 2025-01-29
**Structure Version**: 2.0 (Three-Domain Architecture)
**Maintained By**: Axon Engineering Team

---

**Start Your Journey**:
- Business → [PRODUCT/00-INDEX.md](./PRODUCT/00-INDEX.md)
- Technical → [ENGINEERING/00-START-HERE.md](./ENGINEERING/00-START-HERE.md)
- Process → [PROCESS/00-INDEX.md](./PROCESS/00-INDEX.md)