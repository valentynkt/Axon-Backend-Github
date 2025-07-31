---
id: AXON-20250131-testing-cqrs-behavior-tests-REQUIREMENTS
title: CQRS Behavior Tests: Requirements
module: Testing
feature: CQRSBehaviorTests
gate: G1
owner: spec-analyst
status: draft
relates_to: []
source_of_truth: doc
created: 2025-01-31
updated: 2025-01-31
version: 1
---

# Problem
The current CQRS architecture tests in Axon Backend focus on structural validation (naming conventions, namespace placement, interface compliance) but lack critical behavioral validation. This creates a gap where developers can inadvertently violate CQRS principles through:

- Commands that don't modify state (purely query operations masquerading as commands)
- Queries that perform state mutations (side effects in read operations)
- Missing input validation on commands
- Incorrect return types that break the Result pattern
- Domain entity leakage through query responses

Without behavioral validation, the architecture tests cannot catch violations of core CQRS principles that could lead to confusing, hard-to-debug, and maintenance-heavy code.

# Business Goal
Implement comprehensive CQRS behavioral validation tests that enforce core CQRS principles through static analysis, ensuring:

- **Commands are write operations**: Commands must have observable side effects or state modifications
- **Queries are read-only**: Queries must never modify application state
- **Input validation**: All commands must have corresponding FluentValidation validators
- **Proper return types**: Commands return Result/Result<T>, queries return DTOs
- **Domain boundary protection**: Queries never expose domain entities directly

Success is measured by:
- 100% detection of CQRS behavioral violations through static analysis
- Integration with existing test framework (CqrsPatternRules.cs)
- Clear, actionable error messages for developers
- Zero false positives on current codebase (81/81 tests continue passing)

# Acceptance Criteria

## AC1: Commands_ShouldModify_State Test
**Given** a class that ends with "Command" or resides in a "Commands" namespace  
**When** the behavioral validation is executed  
**Then** the command must demonstrate state modification through:
- Dependency injection of repositories/services that perform state changes
- Method calls to state-changing operations (SaveAsync, AddAsync, UpdateAsync, DeleteAsync)
- Return types indicating potential side effects (Result<T> vs pure DTOs)
- **And** pure query operations (methods returning only data without state changes) should fail validation

## AC2: Queries_ShouldNever_ModifyState Test  
**Given** a class that ends with "Query" or resides in a "Queries" namespace  
**When** the behavioral validation is executed  
**Then** the query must not perform state modifications:
- No dependencies on repositories with write operations
- No method calls to state-changing operations
- Only read-only operations and data retrieval
- **And** any detected state-changing operations should fail validation

## AC3: Commands_ShouldHave_FluentValidationValidators Test
**Given** a command class in the Application layer  
**When** the validation check is executed  
**Then** there must be a corresponding validator:
- Validator class named `{CommandName}Validator` 
- Located in the same namespace as the command
- Inherits from `AbstractValidator<{CommandName}>`
- **And** commands without validators should fail validation

## AC4: Validators_ShouldBeIn_ApplicationLayer Test
**Given** a class that inherits from `AbstractValidator<T>`  
**When** the location validation is executed  
**Then** the validator must:
- Reside in `*.Application.*` namespace
- Be in the same module as the command being validated
- **And** validators in Domain or Infrastructure layers should fail validation

## AC5: Commands_ShouldReturn_ResultOrUnit Test
**Given** a command handler's Handle method  
**When** the return type validation is executed  
**Then** the method must return:
- `Task<Result<T>>` for commands that return data
- `Task<Result>` for commands that only indicate success/failure
- **And** handlers returning primitive types, DTOs directly, or void should fail validation

## AC6: Queries_ShouldReturn_DTOsNotEntities Test
**Given** a query handler's Handle method return type  
**When** the return type analysis is executed  
**Then** the method must return:
- DTO classes (typically ending with "Dto", "Response", or "ViewModel")
- Located in Application layer namespace
- **And** must not return:
  - Domain entities (classes in `*.Domain.*` namespace)
  - Value objects from domain layer
  - Database entities from Infrastructure layer

# Constraints

## Performance
- Static analysis execution time < 5 seconds for entire solution
- Memory usage < 100MB during test execution
- Integration with existing test framework without performance degradation

## Security
- No reflection-based execution of potentially unsafe code
- Analysis limited to type metadata and method signatures
- No dynamic loading of external assemblies

## Compatibility  
- Compatible with current .NET 10 preview and NUnit framework
- Integration with existing ArchitectureTestHelpers and CqrsPatternRules
- Maintains existing test success rate (81/81 tests passing)

## Implementation
- Use NetArchTest.Rules for static analysis where possible
- Extend existing PatternComplianceRule base class
- Follow established patterns in CommandImplementationRule.cs
- Provide clear violation messages with suggested fixes

# Non-Goals

- Runtime behavioral validation (this is for static analysis only)
- Integration testing of actual command/query execution
- Performance testing of command/query handlers
- Database state verification
- Cross-module dependency analysis beyond CQRS patterns

# Assumptions & Risks

## Assumptions
- Current codebase follows established CQRS patterns as foundation
- FluentValidation is the chosen validation framework
- MediatR IRequest/IRequestHandler patterns continue to be used
- Result pattern continues as the standard return type

## Risks
- **False positives** on legitimate patterns not yet seen in codebase
  - *Handling*: Extensive testing against current codebase and conservative detection rules
- **Static analysis limitations** for complex behavioral patterns
  - *Handling*: Focus on detectable patterns through method signatures and dependencies
- **Performance impact** on large codebases during CI/CD
  - *Handling*: Implement caching and optimize reflection usage

# Open Questions

1. Should the validation detect Commands that inject query-only services (like read-only repositories) as violations?
2. How should we handle Commands that perform both read and write operations (e.g., conditional updates)?
3. Should DTOs be required to be in specific namespaces (e.g., `*.DTOs.*` vs `*.Responses.*`)?
4. What's the policy for Commands that return complex domain calculations vs simple success indicators?
5. Should we validate that Query handlers don't inject services with state-changing capabilities, even if unused?