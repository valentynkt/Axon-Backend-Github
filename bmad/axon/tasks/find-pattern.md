# Find Pattern Task

**Agent**: Axon Archaeologist
**Purpose**: Find architectural pattern examples in codebase

---

## Task Instructions

### 5 Core Patterns to Find

1. **Result<T> Pattern**
   ```csharp
   Grep: "Result<.*>" in src/
   Grep: "Result<.*>.Success" and "Result<.*>.Failure"
   Example locations: Domain methods, Application handlers
   ```

2. **StrongId<T> Pattern**
   ```csharp
   Grep: "StrongId<" in src/
   Grep: "record.*Id.*StrongId<"
   Example locations: Domain entities, value objects
   ```

3. **CQRS Pattern**
   ```csharp
   Grep: "IRequest<Result<" in src/Modules/*/Application/
   Grep: "IRequestHandler<.*Command" (Commands)
   Grep: "IRequestHandler<.*Query" (Queries)
   Example locations: Application/Commands/, Application/Queries/
   ```

4. **Domain Events Pattern**
   ```csharp
   Grep: "record.*Event" in src/Modules/*/Domain/
   Grep: "DomainEvent" or "IDomainEvent"
   Example locations: Domain/Events/
   ```

5. **Owned Entity Pattern**
   ```csharp
   Grep: "OwnsOne" or "OwnsMany" in src/Modules/*/Infrastructure/Persistence/
   Grep: "WithOwner" in entity configurations
   Example locations: Infrastructure/Persistence/Configurations/
   ```

### Process

1. **Search for Pattern** (specified pattern type)
2. **Find 3-5 Examples** (best examples across codebase)
3. **Extract Pattern Usage**
   - File path
   - Line numbers
   - Code snippet
   - Context (why used here)

4. **Identify Pattern Variations**
   - Standard usage
   - Edge cases
   - Advanced patterns

### Output Format
```yaml
pattern_examples:
  pattern: "Result<T>"
  examples_found: 5
  usage_count: 127

  best_examples:
    - file: "src/Modules/Identity/Domain/Entities/WalletOwnership.cs"
      line: 45
      context: "Domain method returning Result<Unit> for revocation"
      snippet: |
        public Result<Unit> Revoke(RevokedBy revokedBy)
        {
            if (IsRevoked)
                return Result<Unit>.Failure(WalletOwnershipErrors.AlreadyRevoked);

            RevokedAt = DateTimeOffset.UtcNow;
            RevokedBy = revokedBy;

            return Result<Unit>.Success(Unit.Value);
        }
      pattern_elements:
        - "Returns Result<Unit> for void operations"
        - "Failure with strongly-typed error"
        - "Success with Unit.Value"

    - file: "src/Modules/Identity/Application/Commands/VerifyWalletCommandHandler.cs"
      line: 28
      context: "Application handler propagating Result from domain/infrastructure"
      snippet: |
        public async Task<Result<WalletVerificationResult>> Handle(
            VerifyWalletCommand command,
            CancellationToken cancellationToken)
        {
            var verifyResult = await _verificationService.VerifySignatureAsync(...);
            if (verifyResult.IsFailure)
                return Result<WalletVerificationResult>.Failure(verifyResult.Error);

            // ... create ownership
            return Result<WalletVerificationResult>.Success(result);
        }
      pattern_elements:
        - "Propagates failures early"
        - "Transforms success value"
        - "Result<T> throughout call chain"

  pattern_variations:
    - type: "Result<Unit>"
      usage: "Void operations (commands with no return value)"
      count: 42

    - type: "Result<T>"
      usage: "Operations returning value (queries)"
      count: 85

  anti_patterns_found:
    - file: "src/OldCode/LegacyService.cs"
      line: 15
      issue: "Throws exception instead of returning Result"
      recommendation: "Refactor to return Result<T>"
```

---

## TODO: Full Implementation
Implement pattern search, example extraction, and variation analysis.