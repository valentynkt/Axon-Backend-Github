---
id: AXON-20250803-ArchitectureTests-Security-POLICY_REPORT
title: Security Architecture Tests: Policy Report
module: ArchitectureTests
feature: Security
gate: G3
owner: policy-enforcer
status: draft
relates_to: []
source_of_truth: doc
created: 2025-08-03
updated: 2025-08-03
version: 1
---

# Summary
- Decision: **FAIL**
- Counts: blockers=1, warnings=2, advisory=3

The security architecture tests are failing due to overly broad scope and false positive detection in the DataProtectionRule. The primary issue is scanning system assemblies instead of focusing on application code.

# Architecture & Dependencies
- Findings:
  - No violations found in application architecture dependencies.

# CQRS / MediatR Shape
- Findings:
  - No violations found in CQRS/MediatR implementation.

# Result Pattern & Error Discipline
- Findings:
  - No violations found in Result pattern usage.

# Contracts & DTO Boundaries  
- Findings:
  - No violations found in contract boundaries.

# Security / Secrets / PII
- Findings:
  - **[BLOCKER]** SEC005: DataProtectionRule flagging system assemblies — `ArchitectureTestBase.CreateArchitectureContext()` scans ALL assemblies in AppDomain (line 15), including .NET Framework assemblies. Fix: Filter to application assemblies only.
  - **[WARNING]** False positive logging detection — Rule flags methods like `CreateCultureInfoNoThrow`, `SetAdapterLoggingSettings`, `InvalidTokenError` from system libraries. Fix: Exclude system namespaces (System.*, Microsoft.*).
  - **[WARNING]** Overly aggressive parameter name detection — `IsSensitiveParameterName()` flagging parameters like `useUserOverride`, `keyValue`, `methodToken` which are not actually sensitive. Fix: Refine detection patterns.

# Observability
- Findings:
  - **[ADVISORY]** Application logging patterns are properly structured — Review of `ProcessMessageHandler.cs` shows correct structured logging with no sensitive data exposure.
  - **[ADVISORY]** Proper use of ILogger dependency injection — Application code follows correct logging patterns.

# Build & Analyzers
- Findings:
  - **[ADVISORY]** Architecture test framework has dependency issues — Some tests fail to run due to missing Microsoft.Extensions.Configuration references in test host.

# Performance & Reliability
- Findings:
  - No significant violations found in performance patterns.

# Required Actions

## Blockers to fix before merge:
- `/Users/valentynkit/Repos/Axon-Backend/tests/Axon.ArchitectureTests.Core/TestBase/ArchitectureTestBase.cs:15` — SEC005 — Modify `CreateArchitectureContext()` to filter assemblies:
  ```csharp
  // Before:
  var assemblies = AppDomain.CurrentDomain.GetAssemblies();
  
  // After:
  var assemblies = AppDomain.CurrentDomain.GetAssemblies()
      .Where(a => a.FullName?.StartsWith("Axon.", StringComparison.OrdinalIgnoreCase) == true)
      .ToArray();
  ```

## Warnings to resolve/justify:
- `/Users/valentynkit/Repos/Axon-Backend/tests/Axon.ArchitectureTests.Core/Rules/Security/DataProtectionRule.cs:253-262` — SEC005 — Refine `IsSensitiveParameterName()` to exclude false positives:
  ```csharp
  private static bool IsSensitiveParameterName(string parameterName)
  {
      // Exclude obvious system/framework parameters
      if (string.IsNullOrEmpty(parameterName) || 
          parameterName.StartsWith("method", StringComparison.OrdinalIgnoreCase) ||
          parameterName.Equals("useUserOverride", StringComparison.OrdinalIgnoreCase) ||
          parameterName.Equals("keyValue", StringComparison.OrdinalIgnoreCase))
          return false;
          
      var sensitivePatterns = new[]
      {
          "password", "secret", "apikey", "authtoken", "accesstoken", 
          "ssn", "creditcard", "personalinfo"
      };
      
      var parameterNameLower = parameterName.ToLowerInvariant();
      return sensitivePatterns.Any(pattern => parameterNameLower.Contains(pattern));
  }
  ```

- `/Users/valentynkit/Repos/Axon-Backend/tests/Axon.ArchitectureTests.Core/Rules/Security/DataProtectionRule.cs:24-50` — SEC005 — Add namespace filtering in `ExecuteValidationAsync()`:
  ```csharp
  foreach (var assembly in context.Assemblies)
  {
      // Skip system assemblies
      if (IsSystemAssembly(assembly))
          continue;
          
      var types = assembly.GetTypes()
          .Where(t => !t.IsAbstract && !t.IsInterface);
      // ... rest of validation
  }
  
  private static bool IsSystemAssembly(Assembly assembly)
  {
      var name = assembly.FullName ?? "";
      return name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
             name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
             name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
             name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase);
  }
  ```

# Justifications

## Current State Assessment
The security architecture tests are correctly identifying the need for data protection validation, but the current implementation has scope issues:

1. **Scope Problem**: Tests scan entire AppDomain including system assemblies, generating thousands of false positives
2. **Detection Accuracy**: Parameter name patterns are too broad and flag legitimate system parameters
3. **Application Code Quality**: Actual application code shows proper logging patterns with structured logging and no sensitive data exposure

## Recommended Approach
1. **Fix assembly filtering immediately** (BLOCKER) - This will reduce violations from thousands to actual application-relevant issues
2. **Refine detection patterns** (WARNING) - Improve accuracy while maintaining security vigilance
3. **Consider adding positive patterns** (ADVISORY) - Look for good practices like `[JsonIgnore]`, structured logging, etc.

The underlying security concerns are valid, but the implementation needs scoping fixes to be useful for development teams.