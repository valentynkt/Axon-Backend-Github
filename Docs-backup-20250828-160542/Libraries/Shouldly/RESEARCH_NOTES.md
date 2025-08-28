---
id: AXON-20250730-Libraries-Shouldly-RESEARCH_NOTES
title: Shouldly: Research Notes
module: Libraries
feature: Shouldly
gate: G1
owner: docs-grounder
status: draft
relates_to: []
source_of_truth: doc
created: 2025-07-30
updated: 2025-07-30
version: 1
---

# Questions
- What is the latest version of Shouldly and its compatibility with .NET 10?
- What are the core assertion methods and syntax patterns?
- How does Shouldly integrate with .NET testing frameworks (NUnit, xUnit, MSTest)?
- What are the best practices for using Shouldly in Clean Architecture/CQRS projects?
- Are there any performance considerations or migration guides?
- How does error reporting work and what makes it superior?

# Findings (evidence-backed)

## Latest Version & Compatibility
- **Current Version**: 4.3.0 (released 6 months ago) — [nuget_package]
- **.NET 10 Support**: Compatible via .NET Standard 2.0 and explicit support for .NET 8.0 & 9.0 targets — [nuget_package]
- **Installation**: `dotnet add package Shouldly --version 4.3.0` — [official_docs]

## Core Assertion Philosophy
- **Natural Language Syntax**: Uses extension methods like `value.ShouldBe(expected)` instead of `Assert.That(value, Is.EqualTo(expected))` — [official_docs]
- **Superior Error Messages**: Includes actual code expression in failure messages (e.g., "contestant.Points should be 1337 but was 0") — [official_docs]
- **Code Context**: Uses source code analysis to provide detailed failure context — [official_docs]

## Key Assertion Methods
- **Basic Equality**: `ShouldBe()`, `ShouldNotBe()`, `ShouldBeNull()`, `ShouldNotBeNull()` — [official_docs]
- **Boolean**: `ShouldBeTrue()`, `ShouldBeFalse()` — [official_docs]
- **Numeric**: `ShouldBeGreaterThan()`, `ShouldBeLessThan()`, `ShouldBeInRange()` — [official_docs]
- **String**: `ShouldContain()`, `ShouldStartWith()`, `ShouldEndWith()`, `ShouldNotBeNullOrEmpty()` — [blog_examples]
- **Collections**: `ShouldContain()`, `ShouldNotContain()`, `ShouldAllBe()`, `ShouldBeSubsetOf()` — [blog_examples]
- **Exceptions**: `ShouldThrow<TException>()`, `ShouldNotThrow()`, `ShouldThrowAsync<TException>()` — [blog_examples]
- **Type Checking**: `ShouldBeOfType<T>()`, `ShouldBeAssignableTo<T>()` — [official_docs]

## Testing Framework Integration
- **Universal Compatibility**: Works with NUnit, xUnit, MSTest without framework-specific dependencies — [official_docs]
- **Async Support**: Full async/await support with `ShouldThrowAsync()`, `ShouldNotThrowAsync()` methods — [blog_examples]
- **Multiple Assertions**: `ShouldSatisfyAllConditions()` runs all assertions even if early ones fail — [blog_examples]

## Advanced Features
- **Collection Ordering**: `ShouldBe(expected, ignoreOrder: true)` for order-independent collection comparison — [blog_examples]
- **Approximate Comparisons**: `value.ShouldBe(0.3, 0.00001)` for floating-point tolerance — [secondary_docs]
- **Custom Messages**: All assertions accept optional `customMessage` parameter for additional context — [blog_examples]
- **Build Server Support**: Requires "full" PDB files for optimal error messages in CI/CD — [official_docs]

## Performance & Dependencies
- **Lightweight**: Minimal dependencies (DiffEngine, EmptyFiles, Microsoft.CSharp for .NET Standard 2.0) — [nuget_package]
- **High Adoption**: Over 115 NuGet packages depend on it, used by major projects like ILSpy, Clean Architecture template, Polly — [nuget_package]
- **Active Maintenance**: Maintained by Jake Ginnivan and Joseph Woodward — [official_docs]

# Apply vs Not-Apply (Axon-specific)

## Apply
- **Test Projects**: Ideal for all test assertion scenarios in Axon Backend modules (Chat, Portfolio, etc.)
- **CQRS Testing**: Excellent for testing command/query handlers with `Result<T>` pattern validation
- **Domain Testing**: Perfect for testing domain entities, value objects, and business rules
- **Integration Testing**: Suitable for API endpoint testing and database operation verification
- **Error Scenarios**: Superior error messages align with Result pattern error handling philosophy

## Not-Apply
- **Production Code**: Assertion library is test-only, not for runtime validation
- **Performance-Critical Paths**: Avoid in hot paths due to reflection-based error message generation
- **Legacy Migration**: No immediate need to migrate existing working assertions unless improving readability

# Assumptions & Risks

## Assumptions
- .NET 10 compatibility through .NET Standard 2.0 target will continue
- Source code access required for optimal error messages (satisfied in our development environment)
- Team preference for readable test code over traditional Assert syntax

## Risks
- **Build Server Configuration**: Requires "full" PDB files setting in release configuration for optimal error messages
- **Learning Curve**: Team needs to adopt new syntax patterns, though generally simpler than traditional assertions
- **Debugging Overhead**: Reflection-based error message generation may add minimal test execution time

# Contradictions / Gaps
- Some secondary sources mention version 4.2.1 from April 2025, but NuGet shows 4.3.0 as latest
- Limited official documentation on performance benchmarks compared to built-in assertions
- No official migration guide from NUnit/xUnit built-in assertions to Shouldly

# Citations
- [official_docs]: Shouldly Documentation — https://docs.shouldly.org/ (type: firecrawl)
- [nuget_package]: Shouldly 4.3.0 - NuGet — https://www.nuget.org/packages/shouldly/ (type: firecrawl)  
- [blog_examples]: Getting Started with Shouldly | NimblePros Blog — https://blog.nimblepros.com/blogs/getting-started-with-shouldly/ (type: firecrawl)
- [secondary_docs]: Shouldly Documentation ReadTheDocs — https://shouldly.readthedocs.io/ (type: firecrawl)

# Confidence
High — Official documentation provides comprehensive API coverage, NuGet package confirms .NET compatibility, and practical examples demonstrate real-world usage patterns suitable for Axon's Clean Architecture approach.