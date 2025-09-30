# Doc-Sync - Documentation Synchronization Workflow

**Tier 4 Support Workflow** | Maintains zero documentation drift

Automated drift detection and targeted update generation to keep code and documentation aligned.

---

## 🎯 Purpose

Prevents documentation drift through systematic detection and minimal, targeted updates:
- **Drift Detection** - Identifies 4 drift types (Missing, Outdated, Incorrect, Orphaned)
- **Update Generation** - Creates minimal, targeted documentation updates
- **XML Coverage** - Ensures 100% inline documentation for public APIs
- **Zero Drift** - Maintains alignment between code and documentation

**Core Value**: Documentation stays current without manual effort, architectural decisions remain visible.

---

## 📋 When Invoked

**Invoked by** any implementation workflow at **Phase 3: Validation**:

```yaml
Invoking Workflows:
  - story-implementation (Tier 2)
  - story-refactoring (Tier 2)
  - story-bugfix (Tier 2)
  - identity-workflow (Tier 3)
  - chat-workflow (Tier 3)
  - api-workflow (Tier 3)

Timing: After code generation, before final commit
```

**Example**: story-implementation Phase 3 invokes doc-sync after code is generated to detect drift, generate updates, ensure XML coverage before Checkpoint 4 (Final Commit Approval).

---

## 🔄 Workflow Execution

**2 Agents Execute** (5-10 minutes total):

```
┌────────────────────────────────────────────────┐
│  Doc-Sync (Sequential Execution)              │
├────────────────────────────────────────────────┤
│                                                │
│  Step 1: @axon-doc-oracle (Drift Detection)   │
│  ├─ Detect 4 drift types                      │
│  ├─ Identify affected docs                    │
│  ├─ Assign severity                           │
│  └─ Output: drift-detection.yaml              │
│                                                │
│  Step 2: @axon-doc-oracle (Update Generation) │
│  ├─ Generate before/after docs                │
│  ├─ Provide code references                   │
│  ├─ Specify update types                      │
│  └─ Output: doc-updates.md                    │
│                                                │
│  Step 3: @axon-quality-guardian (XML Check)   │
│  ├─ Validate inline XML coverage              │
│  ├─ Generate missing XML comments             │
│  ├─ Ensure 100% public API docs               │
│  └─ Add to: doc-updates.md                    │
│                                                │
└────────────────────────────────────────────────┘
             ↓
┌────────────────────────────────────────────────┐
│  Doc-Sync Summary                              │
│  - Total Drift: X instances                    │
│  - Affected Docs: Y files                      │
│  - Updates Generated: Z                        │
│  - Zero Drift: Yes | No                        │
└────────────────────────────────────────────────┘
             ↓
     Return to invoking workflow
         (Apply updates before commit)
```

---

## 📊 Drift Types (4 Types)

**@axon-doc-oracle detects**:

### 1. Missing Drift
Documentation doesn't exist for code that exists.

**Examples**:
- New method added but no XML comment
- New API endpoint without API docs
- New domain concept without explanation

**Detection**: Compare code_changes against documentation, find undocumented elements.

---

### 2. Outdated Drift
Documentation exists but describes old behavior.

**Examples**:
- Method signature changed (parameter added) but XML comment unchanged
- Business rule updated but module docs still show old rule
- Authentication flow changed but docs show old flow

**Detection**: Compare implementation_summary against existing docs, find mismatches.

---

### 3. Incorrect Drift
Documentation contradicts actual code behavior.

**Examples**:
- Docs say "returns 200 OK" but code returns 201 Created
- Docs say "throws exception" but code returns Result<T, Error>
- Example code in docs doesn't compile

**Detection**: Compare code behavior against documentation claims, find contradictions.

---

### 4. Orphaned Drift
Documentation exists for code that no longer exists.

**Examples**:
- Docs reference deleted method
- API docs describe removed endpoint
- Architecture docs reference deprecated pattern

**Detection**: Compare documentation references against actual code, find orphans.

---

## 📊 Output Reports

**2 Output Files Generated**:

### 1. Drift Detection Report (drift-detection.yaml)

```yaml
drift_summary:
  missing_count: 2
  outdated_count: 1
  incorrect_count: 0
  orphaned_count: 0
  total_count: 3

drifts_detected:
  - drift_type: Missing
    description: New method VerifyWalletOwnership lacks XML documentation
    code_location: src/Modules/Identity/Domain/Aggregates/AxonPrincipal/Commands.cs:145
    doc_location: Inline XML comment (same file)
    severity: Medium
    recommendation: Add <summary>, <param>, <returns> tags

  - drift_type: Missing
    description: New POST /api/v1/auth/wallet/verify endpoint not in API docs
    code_location: src/Api/Endpoints/V1/Auth/Wallet/VerifyWalletEndpoint.cs:20
    doc_location: Docs/ENGINEERING/modules/identity/05-api-contracts.md
    severity: High
    recommendation: Add endpoint section to API contracts doc

  - drift_type: Outdated
    description: Wallet challenge flow changed but authentication docs unchanged
    code_location: src/Modules/Identity/Application/Commands/GenerateChallenge/
    doc_location: Docs/ENGINEERING/modules/identity/03-authentication.md:120
    severity: Medium
    recommendation: Update challenge flow section with new signing exclusivity check

affected_docs:
  - doc_path: Docs/ENGINEERING/modules/identity/05-api-contracts.md
    sections_affected: [Wallet Verification Endpoints]
    update_type: New
  - doc_path: Docs/ENGINEERING/modules/identity/03-authentication.md
    sections_affected: [Wallet Challenge Flow]
    update_type: Modify

overall_severity: High
```

---

### 2. Documentation Updates (doc-updates.md)

```markdown
# Documentation Updates for AXON-123

## Changes Summary
- Added wallet ownership verification endpoint
- Updated wallet challenge flow with signing exclusivity check
- Added VerifyWalletOwnership method to AxonPrincipal

## Documentation Updates

### Update 1: modules/identity/05-api-contracts.md

**Section**: Wallet Verification Endpoints

**Before**:
(No documentation for this endpoint)

**After**:
```markdown
### POST /api/v1/auth/wallet/verify

Verifies wallet ownership using Ed25519 signature.

**Request**:
```json
{
  "walletAddress": "string (Solana address)",
  "signature": "string (base58)",
  "message": "string"
}
```

**Response (200 OK)**:
```json
{
  "verified": true,
  "ownership": {
    "walletAddress": "...",
    "verifiedAt": "..."
  }
}
```
```

**Reason**: New endpoint added in story AXON-123, API docs missing

**Code Reference**: src/Api/Endpoints/V1/Auth/Wallet/VerifyWalletEndpoint.cs:20

**Update Type**: New

---

### Update 2: modules/identity/03-authentication.md

**Section**: Wallet Challenge Flow (line 120)

**Before**:
```markdown
1. User requests challenge
2. System generates random challenge
3. User signs challenge with wallet
4. System verifies signature
```

**After**:
```markdown
1. User requests challenge
2. System generates random challenge
3. System validates signing exclusivity (one verified+signing wallet per address)
4. User signs challenge with wallet
5. System verifies signature using Ed25519
```

**Reason**: Signing exclusivity check added to challenge flow

**Code Reference**: src/Modules/Identity/Application/Commands/GenerateChallenge/GenerateChallengeHandler.cs:45

**Update Type**: Modify

---

## Inline XML Updates

### File: src/Modules/Identity/Domain/Aggregates/AxonPrincipal/Commands.cs

**Add XML comments (line 145)**:
```csharp
/// <summary>
/// Verifies wallet ownership by validating Ed25519 signature against challenge.
/// Enforces signing exclusivity invariant (one verified+signing wallet per address globally).
/// </summary>
/// <param name="walletAddress">Solana wallet address (base58 format)</param>
/// <param name="signature">Ed25519 signature (base58 format)</param>
/// <param name="message">Challenge message that was signed</param>
/// <returns>
/// Success: Wallet ownership verified and linked
/// Failure: SigningExclusivityViolation, InvalidSignature, WalletAlreadyLinked
/// </returns>
public Result<WalletOwnership, Error> VerifyWalletOwnership(
    WalletAddress walletAddress,
    Ed25519Signature signature,
    ChallengeMessage message)
{
    // Implementation...
}
```

**Location**: src/Modules/Identity/Domain/Aggregates/AxonPrincipal/Commands.cs:145

---

## Validation Checklist
- [ ] All drift instances addressed (3/3)
- [ ] Before/after documentation reviewed
- [ ] Code references accurate (file:line)
- [ ] XML comments complete (1/1 method)
- [ ] Inline XML follows conventions
```

---

## ✅ Documentation Layers (by Module)

**Identity Module** (5 docs):
- 00-INDEX.md - Module overview
- 01-domain-model.md - Aggregates, entities, invariants
- 03-authentication.md - Auth flows, JWT, wallet
- 05-api-contracts.md - API endpoints
- 06-database-schema.md - EF Core configs

**Chat Module** (5 docs):
- 00-INDEX.md - Module overview
- 01-domain-model.md - Conversation aggregate, messages
- 03-messaging-flows.md - Turn-taking, AI integration
- 05-api-contracts.md - API endpoints
- 06-database-schema.md - EF Core configs

**API Layer** (2 docs):
- api/00-INDEX.md - REST conventions
- guides/codebase/coding-standards.md - C# standards

---

## 📊 Success Criteria

- ✅ **Drift Detected**: All 4 types checked (Missing, Outdated, Incorrect, Orphaned)
- ✅ **Drift Count**: 0 OR all drift addressed in updates
- ✅ **Doc Updates Generated**: 100% (all drift has update recommendations)
- ✅ **Inline XML Coverage**: 100% (all public APIs documented)
- ✅ **Execution Time**: ≤ 10 minutes

---

## 🚀 Usage Example

**Scenario**: story-implementation Phase 3 after generating wallet verification endpoint

**Invocation**:
```yaml
Invoking Workflow: story-implementation
Phase: Phase 3 (Validation)
Inputs:
  story_context:
    story_id: AXON-123
    title: Add wallet verification endpoint
    module: Identity + API
  code_changes:
    - src/Modules/Identity/Domain/Aggregates/AxonPrincipal/Commands.cs
    - src/Api/Endpoints/V1/Auth/Wallet/VerifyWalletEndpoint.cs
  implementation_summary: Added VerifyWalletOwnership method, created endpoint
  patterns_used: [Result<T>, StrongId<T>, REPR, Ed25519]
```

**Execution** (8 minutes):
1. **Drift Detection**: Found 3 drift instances (2 Missing, 1 Outdated)
2. **Update Generation**: Created doc-updates.md with 2 doc updates + 1 XML comment
3. **XML Coverage**: Validated 100% coverage (1 new method documented)

**Output**:
```yaml
Doc-Sync Summary:
  Total Drift: 3 instances
  Missing: 2 (new method XML, new endpoint docs)
  Outdated: 1 (challenge flow docs)
  Affected Docs: 2 files (05-api-contracts.md, 03-authentication.md)
  Updates Generated: 3 (2 doc updates, 1 XML comment)
  Zero Drift Achieved: Yes (all drift addressed)

Files to Update:
  - Docs/ENGINEERING/modules/identity/05-api-contracts.md (add section)
  - Docs/ENGINEERING/modules/identity/03-authentication.md (modify section)
  - src/Modules/Identity/Domain/Aggregates/AxonPrincipal/Commands.cs (add XML)
```

**Result**: story-implementation applies updates, proceeds to Checkpoint 4 with zero documentation drift.

---

## 🔗 Related Workflows

**Tier 2 (Core - Invokers)**:
- `story-implementation` - Invokes at Phase 3
- `story-refactoring` - Invokes at Phase 3
- `story-bugfix` - Invokes at Phase 3

**Tier 3 (Module - Invokers)**:
- `identity-workflow` - Invokes at Enhancement Point 4
- `chat-workflow` - Invokes at Enhancement Point 4
- `api-workflow` - Invokes at Enhancement Point 4

**Tier 4 (Support - Siblings)**:
- `pre-flight-validation` - Parallel validation before implementation

---

## 📖 Configuration

**Location**: `bmad/axon/workflows/doc-sync/workflow.yaml`

**Key Variables**:
- `drift_types`: 4 types (Missing, Outdated, Incorrect, Orphaned)
- `doc_layers_identity`: 5 Identity docs
- `doc_layers_chat`: 5 Chat docs
- `doc_layers_api`: 2 API docs
- `update_types`: 4 types (New, Modify, Delete, Inline)

**Invocation**:
```yaml
# From any implementation workflow
doc-sync:
  inputs:
    - story_context
    - code_changes
    - implementation_summary
    - patterns_used
  outputs:
    - drift-detection.yaml
    - doc-updates.md
  duration: 5-10 minutes
```

---

## 🛠️ Troubleshooting

**Issue**: Drift detection finds false positives
- Check: Implementation summary accurate?
- Check: Code changes list complete?
- Fix: Refine implementation summary, ensure all changed files listed

**Issue**: Updates too verbose
- Check: Minimal update strategy followed?
- Fix: Focus on changed sections only, avoid full rewrites

**Issue**: XML coverage < 100%
- Check: All public APIs in code_changes list?
- Check: Internal methods incorrectly marked public?
- Fix: Add missing XML comments, adjust accessibility modifiers

**Issue**: Zero drift not achieved
- Check: drift-detection.yaml drift count
- Check: doc-updates.md has updates for all drift?
- Fix: Generate missing updates, address all drift instances

---

**Version:** 1.0.0  
**Last Updated:** 2025-09-30  
**Status:** ✅ Production-Ready (BMM Token-Efficient Pattern)