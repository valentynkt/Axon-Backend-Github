# Architecture Tests for Testing Library Standards

This project contains comprehensive architecture tests that enforce our testing library standards across the entire Axon Backend solution. These tests ensure consistency, prevent regression, and maintain code quality by validating that all test projects adhere to our standardized testing approach.

## Purpose

These architecture tests serve as **build-time enforcement** to ensure:

- **Prohibited Libraries**: No usage of XUnit or FluentAssertions
- **Required Libraries**: All test projects use NUnit + Shouldly
- **Code Quality**: Proper test structure, naming conventions, and patterns
- **Project Structure**: Correct project configuration and dependencies

## Test Categories

### 1. TestingLibraryEnforcementTests

**Primary enforcement tests** that validate library usage:

- ✅ **Prohibited Libraries Detection**
  - No XUnit framework references
  - No XUnit runner dependencies  
  - No FluentAssertions references
  - No XUnit attributes in test methods

- ✅ **Required Libraries Validation**
  - All test assemblies reference NUnit
  - All test assemblies reference Shouldly
  - Test methods use NUnit attributes
  - Shouldly assertion syntax usage

- ✅ **Code Usage Detection**
  - No FluentAssertions extension method calls
  - Proper Shouldly assertion patterns

### 2. TestProjectStructureTests

**Project configuration validation**:

- Correct SDK references (`Microsoft.NET.Sdk`)
- Proper test project markers (`<IsTestProject>true</IsTestProject>`)
- Required NuGet package references
- Global using statements for NUnit and Shouldly
- Naming convention compliance
- Build configuration validation

### 3. TestCodeQualityTests

**Test code quality enforcement**:

- Test class naming conventions (`*Tests`)
- Required `[TestFixture]` attributes
- Public test method visibility
- Proper async test patterns
- Descriptive test method naming
- No hardcoded waits/delays
- Appropriate base class usage

## Why NUnit + Shouldly?

Based on our research (see `@Docs/features/Libraries/Shouldly/`):

### NUnit Advantages
- Mature, stable testing framework
- Excellent async/await support
- Rich assertion model
- Superior test runner performance
- Better IDE integration

### Shouldly Advantages  
- **Natural language syntax**: `value.ShouldBe(expected)` vs `Assert.That(value, Is.EqualTo(expected))`
- **Superior error messages**: Includes actual code expression in failures
- **Code context**: Uses source code analysis for detailed failure context
- **Universal compatibility**: Works with any .NET testing framework

## Build Integration

These tests are configured to **fail the build** when violations are detected:

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

### Running the Tests

```bash
# Run all architecture tests
dotnet test tests/ArchitectureTests/

# Run specific test category
dotnet test tests/ArchitectureTests/ --filter "FullyQualifiedName~TestingLibraryEnforcementTests"

# Include in CI/CD pipeline
dotnet test --configuration Release --no-build --verbosity normal
```

## Enforcement Rules

### ❌ Prohibited Patterns

```csharp
// DON'T - XUnit usage
[Fact]
public void SomeTest() { }

// DON'T - FluentAssertions usage  
result.Should().Be(expected);

// DON'T - Hardcoded waits
Thread.Sleep(1000);
await Task.Delay(500);
```

### ✅ Required Patterns

```csharp
// DO - NUnit + Shouldly
[TestFixture]
public class ProcessMessageHandlerTests
{
    [Test]
    public void ProcessMessage_GivenValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var command = new ProcessMessageCommand("test");
        
        // Act
        var result = handler.Handle(command);
        
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("expected");
    }
}
```

## Migration Support

For teams migrating from XUnit + FluentAssertions:

1. **Replace test attributes**:
   - `[Fact]` → `[Test]`
   - `[Theory]` → `[TestCase]` or `[TestCaseSource]`
   - `[InlineData]` → `[TestCase]`

2. **Replace assertion syntax**:
   - `Assert.Equal(expected, actual)` → `actual.ShouldBe(expected)`
   - `result.Should().BeTrue()` → `result.ShouldBeTrue()`
   - `list.Should().Contain(item)` → `list.ShouldContain(item)`

3. **Update project references**:
   - Remove XUnit packages
   - Add NUnit + Shouldly packages
   - Add global using statements

## Continuous Improvement

These architecture tests evolve with our standards:

- **Regular Updates**: New rules added as standards develop
- **Team Feedback**: Rules refined based on developer experience  
- **Documentation**: Keep in sync with `@Docs/Claude/` guidance
- **Performance**: Monitor test execution time and optimize

## Troubleshooting

### Common Issues

1. **"Assembly not found" errors**
   - Ensure all test projects are built before running architecture tests
   - Check project references in `Axon.ArchitectureTests.csproj`

2. **False positives in enforcement tests**
   - Review exclusion logic in helper methods
   - Verify assembly loading patterns

3. **Performance issues**
   - Architecture tests load multiple assemblies - expect longer execution time
   - Run in parallel where possible
   - Cache architecture loading in `OneTimeSetUp`

### Support

For questions or issues with architecture tests:

1. Check existing test failures for guidance
2. Review the test implementation for specific rules
3. Consult `@Docs/features/Libraries/Shouldly/` for assertion guidance
4. Update this documentation when adding new rules

---

**Remember**: These tests are your safety net. They catch violations before they reach production and ensure consistency across the entire development team.