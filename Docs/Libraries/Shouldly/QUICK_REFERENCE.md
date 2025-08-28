---
id: AXON-20250730-Libraries-Shouldly-QUICK_REFERENCE
title: Shouldly Quick Reference
module: Libraries
feature: Shouldly
owner: docs-grounder
status: draft
created: 2025-07-30
updated: 2025-07-30
version: 1
---

# Shouldly Quick Reference

## Installation
```bash
dotnet add package Shouldly --version 4.3.0
```

## Basic Assertions

### Equality
```csharp
value.ShouldBe(expected);
value.ShouldNotBe(unexpected);
```

### Null Checks
```csharp
value.ShouldBeNull();
value.ShouldNotBeNull();
```

### Boolean
```csharp
condition.ShouldBeTrue();
condition.ShouldBeFalse();
```

### Numeric
```csharp
number.ShouldBeGreaterThan(5);
number.ShouldBeLessThan(10);
number.ShouldBeGreaterThanOrEqualTo(5);
number.ShouldBeLessThanOrEqualTo(10);
number.ShouldBeInRange(1, 100);
number.ShouldBePositive();
number.ShouldBeNegative();

// Floating point with tolerance
value.ShouldBe(expected, tolerance);
```

### Strings
```csharp
text.ShouldContain("substring");
text.ShouldNotContain("unwanted");
text.ShouldStartWith("prefix");
text.ShouldEndWith("suffix");
text.ShouldBeNullOrEmpty();
text.ShouldNotBeNullOrEmpty();
text.ShouldNotBeNullOrWhiteSpace();

// Case insensitive
text.ShouldContain("SUBSTRING", Case.Insensitive);
```### Collections
```csharp
collection.ShouldBeEmpty();
collection.ShouldNotBeEmpty();
collection.ShouldContain(item);
collection.ShouldNotContain(item);
collection.ShouldContain(x => x.Property == value);
collection.ShouldAllBe(x => x.IsValid);
collection.ShouldBeSubsetOf(largerCollection);

// Count/Length
collection.Count.ShouldBe(expectedCount);
collection.Length.ShouldBe(expectedLength);

// Order-sensitive comparison
actual.ShouldBe(expected);

// Order-insensitive comparison  
actual.ShouldBe(expected, ignoreOrder: true);
```

### Exceptions
```csharp
// Sync
Action action = () => methodCall();
action.ShouldThrow<ExceptionType>();
action.ShouldNotThrow();

// With message check
var ex = action.ShouldThrow<ArgumentException>();
ex.Message.ShouldContain("expected text");

// Async
Func<Task> asyncAction = () => asyncMethod();
await asyncAction.ShouldThrowAsync<ExceptionType>();
await asyncAction.ShouldNotThrowAsync();
```

### Types
```csharp
obj.ShouldBeOfType<SpecificType>();
obj.ShouldBeAssignableTo<BaseType>();
obj.ShouldNotBeOfType<WrongType>();
```

### Multiple Assertions
```csharp
result.ShouldSatisfyAllConditions(
    () => result.Property1.ShouldBe(value1),
    () => result.Property2.ShouldBe(value2),
    () => result.Property3.ShouldNotBeNull()
);
```

## CQRS/Result Pattern
```csharp
// Success cases
result.IsSuccess.ShouldBeTrue();
result.Value.ShouldNotBeNull();
result.Value.ShouldBe(expectedValue);

// Failure cases  
result.IsFailure.ShouldBeTrue();
result.Error.ShouldNotBeNull();
result.Error.Code.ShouldBe("ERROR_CODE");
result.Error.Message.ShouldContain("expected text");
```

## Custom Messages
```csharp
// Add context to any assertion
value.ShouldBe(expected, "Custom failure message");
condition.ShouldBeTrue("This should be true because...");
```

## Common Extensions for Axon
```csharp
// Custom extension methods
public static class ShouldlyExtensions
{
    public static void ShouldBeValidResult<T>(this Result<T> result)
    {
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }
    
    public static void ShouldBeValidationError(this Result result)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }
}
```