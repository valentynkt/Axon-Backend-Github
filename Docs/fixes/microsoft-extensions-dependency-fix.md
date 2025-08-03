# Microsoft.Extensions.Configuration Dependency Fix

## Problem Summary

The Axon.ArchitectureTests.Framework project was experiencing critical dependency issues that prevented architecture tests from executing:

**Primary Issue**: Microsoft.Extensions.Configuration v9.0.0 dependency causing test execution failure with message:
```
An assembly specified in the application dependencies manifest (Axon.ArchitectureTests.Framework.deps.json) was not found:
package: 'Microsoft.Extensions.Configuration', version: '9.0.0'
path: 'lib/net9.0/Microsoft.Extensions.Configuration.dll'
```

**Root Cause**: Version/path mismatch - the project targeted `net10.0` but was using v9.0.0 packages that expected `lib/net9.0/` paths instead of `lib/net10.0/` paths.

**Secondary Issue**: Duplicate key error with "NUnit3.TestAdapter" in ArchitectureContext preventing test execution even after dependency fix.

## Solution Implemented

### 1. Dependency Version Updates

Updated all Microsoft.Extensions packages in `Axon.ArchitectureTests.Framework.csproj` to .NET 10.0 preview versions:

**Before:**
```xml
<PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
```

**After:**
```xml
<PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.0-preview.5.25277.114" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.0-preview.5.25277.114" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.0-preview.5.25277.114" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0-preview.5.25277.114" />
```

### 2. Test Project Consistency

Updated `Axon.ArchitectureTests.Framework.Tests.csproj` to use matching versions:

```xml
<PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.0-preview.5.25277.114" />
<PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.0-preview.5.25277.114" />
```

### 3. Duplicate Key Fix

Fixed the duplicate assembly key issue in `ArchitectureContext.cs`:

**Before (causing duplicate key exception):**
```csharp
_typesByAssembly = _assemblies.ToDictionary(
    a => a.GetName().Name ?? "Unknown",
    a => (IReadOnlyList<Type>)a.GetTypes().ToList());
```

**After (handles duplicate assembly names):**
```csharp
_typesByAssembly = _assemblies
    .GroupBy(a => a.GetName().Name ?? "Unknown")
    .ToDictionary(
        g => g.Key,
        g => (IReadOnlyList<Type>)g.SelectMany(a => a.GetTypes()).ToList());
```

## Verification

### Build Success
```bash
dotnet build tests/Axon.ArchitectureTests.Framework/ --verbosity minimal
# Result: Build succeeded, 0 Warning(s), 0 Error(s)

dotnet build tests/Axon.ArchitectureTests.Core/ --verbosity minimal  
# Result: Build succeeded, 0 Warning(s), 0 Error(s)
```

### Test Execution Success
```bash
dotnet test tests/Axon.ArchitectureTests.Core/ --filter "FullyQualifiedName~AppSettings"
# Result: Test runs successfully (may fail on business logic, but no longer fails on dependency loading)
```

## Prevention Measures

### 1. Version Consistency Rules

- Always use .NET 10.0 preview packages (`10.0.0-preview.5.25277.114`) for .NET 10.0 targeted projects
- Ensure all Microsoft.Extensions.* packages use the same version within a project
- Check that transitive dependencies don't downgrade package versions

### 2. Build Validation

Add to CI/CD pipeline:
```bash
# Verify no package downgrades
dotnet restore --verbosity normal | grep -i "downgrade"

# Verify all architecture tests can execute 
dotnet test tests/Axon.ArchitectureTests.Core/ --logger:console;verbosity=minimal
```

### 3. Dependency Audit

Regularly audit project dependencies:
```bash
dotnet list package --include-transitive | grep "Microsoft.Extensions.Configuration"
```

## Files Modified

1. `/tests/Axon.ArchitectureTests.Framework/Axon.ArchitectureTests.Framework.csproj`
   - Updated 4 Microsoft.Extensions package versions to 10.0.0-preview.5.25277.114

2. `/tests/Axon.ArchitectureTests.Framework.Tests/Axon.ArchitectureTests.Framework.Tests.csproj`
   - Updated 2 Microsoft.Extensions package versions to 10.0.0-preview.5.25277.114

3. `/tests/Axon.ArchitectureTests.Framework/Engine/ArchitectureContext.cs`
   - Fixed duplicate key handling in assembly dictionary creation

## Impact

- ✅ Architecture tests can now execute successfully
- ✅ No more dependency loading failures
- ✅ Framework properly handles duplicate assembly names
- ✅ Consistent .NET 10.0 dependency versions across test projects
- ✅ All builds complete without errors or warnings

## Rollback Plan

If issues arise, revert by:
1. Restore original package versions (9.0.0) in both csproj files
2. Revert ArchitectureContext.cs to original ToDictionary implementation
3. Consider targeting net9.0 instead of net10.0 if preview packages cause instability

## Related Issues

This fix resolves the core dependency issue that was preventing architecture validation, which is critical for maintaining clean architecture principles in the Axon Backend system.