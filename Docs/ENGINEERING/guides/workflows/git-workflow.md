# Git Workflow

**Branch naming, commit messages, and pull request process.**

---

**STATUS**: 🚧 Draft - AI Content Generation Ready
**PRIORITY**: Medium
**LAST_UPDATED**: 2025-09-29

---

## Overview

Axon Backend follows a structured Git workflow to maintain code quality, enable effective collaboration, and provide clear project history. This guide covers branch naming, commit conventions, and the PR process.

---

## Branch Strategy

### Main Branches

**`main`** - Production-ready code
- Always stable and deployable
- Protected branch (requires PR review)
- Tagged with semantic versions
- Direct commits forbidden

**`dev`** - Integration branch
- Latest development work
- Feature branches merge here first
- Protected branch (requires PR review)
- Automated CI/CD pipeline

### Branch Naming Convention

```
{type}/{module}-{short-description}

Examples:
feature/identity-wallet-verification
feature/chat-message-reactions
fix/identity-null-reference-credential
fix/chat-message-ordering
refactor/identity-repository-cleanup
chore/upgrade-ef-core-9
docs/identity-authentication-guide
test/chat-concurrency-scenarios
```

**Branch Types:**
- `feature/` - New functionality
- `fix/` - Bug fixes
- `refactor/` - Code refactoring (no behavior change)
- `chore/` - Dependency updates, tooling
- `docs/` - Documentation only
- `test/` - Test improvements
- `perf/` - Performance improvements

**Module Prefix (when applicable):**
- `identity-` - Identity module changes
- `chat-` - Chat module changes
- `api-` - API layer changes
- `infra-` - Infrastructure/BuildingBlocks changes

---

## Commit Message Convention

### Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Type
- `feat` - New feature
- `fix` - Bug fix
- `refactor` - Code refactoring
- `test` - Adding/updating tests
- `docs` - Documentation changes
- `style` - Code style/formatting (no logic change)
- `perf` - Performance improvements
- `chore` - Build, dependencies, tooling

### Scope (optional but recommended)
- Module name: `identity`, `chat`, `api`
- Area: `auth`, `db`, `validation`, `caching`

### Subject
- Use imperative mood: "add" not "added" or "adds"
- Lowercase first letter
- No period at the end
- Max 50 characters

### Examples

**Good commits:**
```bash
feat(identity): add wallet signature verification

Implements Ed25519 signature verification for Solana wallets.
Adds WalletVerificationService with NSec.Cryptography integration.

Closes #123

---

fix(chat): resolve message ordering race condition

Messages were appearing out of order due to CreatedAt timestamp
precision issues. Changed to use sequential MessageId ordering.

Fixes #456

---

refactor(identity): extract principal resolution to service

Moved complex principal resolution logic from command handler
to dedicated PrincipalResolutionService for better testability.

---

test(identity): add concurrency tests for credential exchange

Added tests covering concurrent credential exchange scenarios
to ensure thread-safety of principal resolution.

---

docs(identity): update authentication flow diagrams

Updated sequence diagrams to reflect new wallet verification
process with challenge/response pattern.
```

**Bad commits:**
```bash
# ❌ Too vague
"fixed bug"
"updates"
"changes"

# ❌ No context
"fix"
"wip"
"stuff"

# ❌ Multiple concerns
"feat: add wallet verification, fix null ref, update docs"

# ❌ Past tense
"added wallet verification"
"fixed the bug"
```

---

## Pull Request Process

### 1. Before Creating PR

**Checklist:**
```bash
# 1. Rebase on latest dev
git checkout dev
git pull origin dev
git checkout your-branch
git rebase dev

# 2. Ensure all tests pass
dotnet test

# 3. Ensure build succeeds (Release mode)
dotnet build --configuration Release

# 4. Format code
dotnet format

# 5. Review your own changes
git diff dev...your-branch

# 6. Update documentation (if needed)
# Check Docs/ENGINEERING/ for relevant files
```

### 2. Create Pull Request

**PR Title Format:**
```
[Module] Brief description of changes

Examples:
[Identity] Add wallet signature verification
[Chat] Fix message ordering race condition
[API] Implement rate limiting middleware
[Infrastructure] Upgrade to EF Core 9.0
```

**PR Description Template:**
```markdown
## Summary
[Brief description of what this PR accomplishes]

## Changes
- [ ] Added wallet signature verification endpoint
- [ ] Implemented Ed25519SignatureVerifier service
- [ ] Added WalletVerificationService with challenge generation
- [ ] Updated AxonPrincipal aggregate to track wallet proofs

## Testing
- [ ] Unit tests added for Ed25519SignatureVerifier
- [ ] Integration tests for VerifyWalletCommand
- [ ] E2E test for full verification flow
- [ ] All existing tests passing

## Documentation
- [ ] Updated Identity module authentication guide
- [ ] Added API contract documentation
- [ ] Updated Swagger annotations

## Related Issues
Closes #123
Related to #456

## Breaking Changes
None / [Describe any breaking changes]

## Migration Required
No / [Describe migration steps if needed]

## Screenshots / Logs
[If applicable, add screenshots or relevant logs]
```

### 3. PR Review Process

**Reviewer Checklist:**

**Architecture & Design:**
- [ ] Changes follow Clean Architecture principles
- [ ] CQRS pattern used correctly (commands vs queries)
- [ ] Result<T, Error> used instead of exceptions
- [ ] StrongId<T> used for entity identifiers
- [ ] Module boundaries respected

**Code Quality:**
- [ ] Modern C# conventions followed (file-scoped namespaces, records)
- [ ] No code smells (long methods, god classes, etc.)
- [ ] Business logic in domain layer (not services/handlers)
- [ ] Proper dependency injection
- [ ] Null reference warnings addressed

**Testing:**
- [ ] Unit tests cover business logic
- [ ] Integration tests for application layer
- [ ] E2E tests for critical paths
- [ ] Test coverage >= 90%
- [ ] Edge cases tested

**Performance:**
- [ ] No N+1 query issues
- [ ] Appropriate indexes on database columns
- [ ] Caching strategy considered
- [ ] Async/await used correctly

**Security:**
- [ ] No sensitive data in logs
- [ ] Input validation implemented
- [ ] Authorization checks present
- [ ] SQL injection prevented (EF Core parameterized queries)

**Documentation:**
- [ ] Public APIs documented with XML comments
- [ ] Complex logic has inline comments
- [ ] README/guides updated if needed
- [ ] Migration guide provided (if breaking change)

### 4. Addressing Feedback

```bash
# Make changes based on review
vim src/Modules/Identity/...

# Commit changes
git add .
git commit -m "refactor(identity): address PR feedback - extract validation logic"

# Push changes
git push origin your-branch

# PR automatically updates
```

### 5. Merge Strategy

**Squash and Merge (Default):**
- Used for most PRs
- Creates single commit on target branch
- Keeps `dev` history clean

**Merge Commit (Rare):**
- Used for complex features with meaningful commit history
- Preserves all commits from feature branch

**Rebase and Merge (Very Rare):**
- Used for very simple, single-commit PRs

---

## Common Workflows

### Feature Development

```bash
# 1. Create branch from dev
git checkout dev
git pull origin dev
git checkout -b feature/identity-wallet-verification

# 2. Implement feature with atomic commits
git add src/Modules/Identity/Domain/
git commit -m "feat(identity): add WalletProof value object"

git add src/Modules/Identity/Application/
git commit -m "feat(identity): implement VerifyWalletCommand"

git add src/Api/Endpoints/
git commit -m "feat(identity): add wallet verification endpoint"

# 3. Push to remote
git push origin feature/identity-wallet-verification

# 4. Create PR on GitHub
# Use PR template

# 5. Address review feedback
# Make changes, commit, push

# 6. Squash and merge after approval
```

### Bug Fix

```bash
# 1. Create fix branch
git checkout dev
git pull origin dev
git checkout -b fix/chat-message-ordering

# 2. Write failing test
git add tests/Modules/Chat/
git commit -m "test(chat): add test for message ordering"

# 3. Fix the bug
git add src/Modules/Chat/
git commit -m "fix(chat): use MessageId for consistent ordering"

# 4. Push and create PR
git push origin fix/chat-message-ordering

# 5. Reference issue in PR: "Fixes #456"
```

### Hotfix (Critical Production Bug)

```bash
# 1. Create hotfix branch from main
git checkout main
git pull origin main
git checkout -b hotfix/critical-auth-bypass

# 2. Fix the issue
git add src/
git commit -m "fix(identity): prevent authentication bypass via null check"

# 3. Push and create PR to main
git push origin hotfix/critical-auth-bypass

# 4. After merge to main, cherry-pick to dev
git checkout dev
git pull origin dev
git cherry-pick <commit-hash>
git push origin dev

# 5. Tag release on main
git checkout main
git tag -a v1.0.1 -m "Hotfix: Critical auth bypass"
git push origin v1.0.1
```

---

## Code Review Best Practices

### For Authors

✅ **DO:**
- Keep PRs small (< 400 lines of changes)
- Provide context in description
- Self-review before requesting review
- Respond to feedback promptly
- Explain complex decisions in comments

❌ **DON'T:**
- Submit PRs with failing tests
- Include unrelated changes
- Force-push after review started
- Take feedback personally

### For Reviewers

✅ **DO:**
- Review within 24 hours
- Provide constructive feedback
- Suggest improvements, don't just criticize
- Ask questions if unclear
- Approve when standards are met

❌ **DON'T:**
- Nitpick minor style issues (use linters)
- Block PR for personal preferences
- Approve without reading code
- Be condescending in comments

---

## Git Commands Reference

### Common Operations

```bash
# Update local dev with remote
git checkout dev
git pull origin dev

# Rebase feature branch on latest dev
git checkout feature/my-feature
git rebase dev

# Interactive rebase to clean history
git rebase -i HEAD~3

# Amend last commit
git add .
git commit --amend --no-edit

# Undo last commit (keep changes)
git reset --soft HEAD~1

# Discard uncommitted changes
git checkout -- .

# View commit history
git log --oneline --graph --all

# Find commits that changed a file
git log --follow -- src/Modules/Identity/Domain/AxonPrincipal.cs
```

### Conflict Resolution

```bash
# When rebase has conflicts
git status  # See conflicting files

# Edit conflicting files
# Resolve <<<< ==== >>>> markers

# Mark as resolved
git add .
git rebase --continue

# Abort rebase if needed
git rebase --abort
```

---

## Related Documentation

- **Development Workflow** → [development-workflow.md](./development-workflow.md)
- **Getting Started** → [getting-started.md](./getting-started.md)
- **Coding Standards** → [../codebase/coding-standards.md](../codebase/coding-standards.md)
- **Testing Workflow** → [testing-workflow.md](./testing-workflow.md)

---

**Last Updated**: 2025-09-29