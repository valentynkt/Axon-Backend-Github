# CQRS Behavior Tests - Technical Specifications

## Implementation Integration with Existing Framework

All behavioral tests will integrate with the existing architecture test framework by:

1. **Extending CqrsPatternRules.cs** - Add new test methods following existing patterns
2. **Using ArchitectureTestHelpers** - Leverage existing reflection utilities and violation formatting
3. **Following established patterns** - Mirror structure of existing CommandImplementationRule.cs
4. **Maintaining performance** - Use cached reflection and efficient type analysis

## Test 1: Commands_ShouldModify_State

### Detection Strategy
```csharp
[Test]
public void Commands_ShouldModify_State()
{
    var commandHandlerTypes = GetCommandHandlerTypes();
    var violations = new List<string>();

    foreach (var handlerType in commandHandlerTypes)
    {
        if (!HasStateModifyingCapabilities(handlerType))
        {
            violations.Add($"{handlerType.FullName} - Command handler lacks state modification capabilities");
        }
    }

    violations.ShouldBeEmpty(ArchitectureTestHelpers.FormatViolations(violations, 
        "Command handlers must demonstrate state modification through dependencies or operations"));
}

private bool HasStateModifyingCapabilities(Type handlerType)
{
    // Check constructor dependencies for state-changing services
    var constructors = handlerType.GetConstructors();
    foreach (var constructor in constructors)
    {
        var parameters = constructor.GetParameters();
        if (parameters.Any(p => IsStateModifyingService(p.ParameterType)))
            return true;
    }
    
    // Check method calls in Handle method for state operations
    var handleMethod = handlerType.GetMethod("Handle");
    if (handleMethod != null && ReturnsResultType(handleMethod))
    {
        // Commands returning Result<T> typically indicate state operations
        return true;
    }
    
    return false;
}

private bool IsStateModifyingService(Type serviceType)
{
    var serviceName = serviceType.Name;
    
    // Repository patterns (write operations)
    if (serviceName.Contains("Repository") && !serviceName.Contains("ReadOnly"))
        return true;
    
    // Domain services that typically modify state
    if (serviceName.Contains("Service") && serviceType.Namespace?.Contains("Domain") == true)
        return true;
        
    // Unit of work pattern
    if (serviceName.Contains("UnitOfWork") || serviceName.Contains("DbContext"))
        return true;
    
    return false;
}
```

### Error Messages
- **Violation**: `"Command handler '{HandlerName}' lacks state modification capabilities - Commands should modify application state"`
- **Suggested Fix**: `"Inject repositories, domain services, or other state-changing dependencies, or consider converting to a Query"`

---

## Test 2: Queries_ShouldNever_ModifyState

### Detection Strategy  
```csharp
[Test]
public void Queries_ShouldNever_ModifyState()
{
    var queryHandlerTypes = GetQueryHandlerTypes();
    var violations = new List<string>();

    foreach (var handlerType in queryHandlerTypes)
    {
        var stateModifyingDependencies = GetStateModifyingDependencies(handlerType);
        if (stateModifyingDependencies.Any())
        {
            violations.Add($"{handlerType.FullName} - Query handler injects state-modifying dependencies: {string.Join(", ", stateModifyingDependencies)}");
        }
    }

    violations.ShouldBeEmpty(ArchitectureTestHelpers.FormatViolations(violations,
        "Query handlers must not have dependencies that can modify state"));
}

private IEnumerable<string> GetStateModifyingDependencies(Type handlerType)
{
    var stateModifyingDeps = new List<string>();
    var constructors = handlerType.GetConstructors();
    
    foreach (var constructor in constructors)
    {
        var parameters = constructor.GetParameters();
        foreach (var param in parameters)
        {
            if (IsStateModifyingService(param.ParameterType))
            {
                stateModifyingDeps.Add(param.ParameterType.Name);
            }
        }
    }
    
    return stateModifyingDeps;
}
```

### Error Messages
- **Violation**: `"Query handler '{HandlerName}' injects state-modifying dependencies: {DependencyList} - Queries should only read data"`
- **Suggested Fix**: `"Replace with read-only alternatives or consider converting to a Command if state modification is intended"`

---

## Test 3: Commands_ShouldHave_FluentValidationValidators

### Detection Strategy
```csharp
[Test]
public void Commands_ShouldHave_FluentValidationValidators()
{
    var commandTypes = GetCommandTypes();
    var violations = new List<string>();

    foreach (var commandType in commandTypes)
    {
        var expectedValidatorName = $"{commandType.Name}Validator";
        var validator = FindValidatorForCommand(commandType, expectedValidatorName);
        
        if (validator == null)
        {
            violations.Add($"{commandType.FullName} - Missing validator '{expectedValidatorName}'");
        }
        else if (!IsInSameNamespace(commandType, validator))
        {
            violations.Add($"{commandType.FullName} - Validator '{validator.FullName}' should be in same namespace");
        }
    }

    violations.ShouldBeEmpty(ArchitectureTestHelpers.FormatViolations(violations,
        "Commands must have corresponding FluentValidation validators"));
}

private Type? FindValidatorForCommand(Type commandType, string expectedValidatorName)
{
    // Search in same namespace first
    var sameNamespaceTypes = Types.InCurrentDomain()
        .That()
        .ResideInNamespace(commandType.Namespace!)
        .And()
        .HaveNameEndingWith("Validator")
        .GetTypes();
    
    var exactMatch = sameNamespaceTypes.FirstOrDefault(t => t.Name == expectedValidatorName);
    if (exactMatch != null && InheritsFromAbstractValidator(exactMatch, commandType))
        return exactMatch;
    
    return null;
}

private bool InheritsFromAbstractValidator(Type validatorType, Type commandType)
{
    var baseType = validatorType.BaseType;
    if (baseType?.IsGenericType == true)
    {
        var genericTypeDef = baseType.GetGenericTypeDefinition();
        if (genericTypeDef.Name.Contains("AbstractValidator"))
        {
            var genericArgs = baseType.GetGenericArguments();
            return genericArgs.Length == 1 && genericArgs[0] == commandType;
        }
    }
    return false;
}
```

### Error Messages
- **Violation**: `"Command '{CommandName}' missing validator '{ExpectedValidatorName}' - All commands require input validation"`
- **Suggested Fix**: `"Create validator class '{ExpectedValidatorName}' inheriting from AbstractValidator<{CommandName}> in the same namespace"`

---

## Test 4: Validators_ShouldBeIn_ApplicationLayer

### Detection Strategy
```csharp
[Test]
public void Validators_ShouldBeIn_ApplicationLayer()
{
    var validatorTypes = Types.InCurrentDomain()
        .That()
        .Inherit(typeof(FluentValidation.AbstractValidator<>))
        .GetTypes();

    var violations = new List<string>();

    foreach (var validatorType in validatorTypes)
    {
        if (!IsInApplicationLayer(validatorType))
        {
            violations.Add($"{validatorType.FullName} - Validator not in Application layer");
        }
    }

    violations.ShouldBeEmpty(ArchitectureTestHelpers.FormatViolations(violations,
        "FluentValidation validators must be in Application layer"));
}

private bool IsInApplicationLayer(Type type)
{
    return type.Namespace?.Contains(".Application.") == true;
}
```

### Error Messages
- **Violation**: `"Validator '{ValidatorName}' not in Application layer - Validators should be co-located with their commands"`
- **Suggested Fix**: `"Move validator to Application layer in same namespace as the command it validates"`

---

## Test 5: Commands_ShouldReturn_ResultOrUnit

### Detection Strategy
```csharp
[Test]  
public void Commands_ShouldReturn_ResultOrUnit()
{
    var commandHandlerTypes = GetCommandHandlerTypes();
    var violations = new List<string>();

    foreach (var handlerType in commandHandlerTypes)
    {
        var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Handle");

        foreach (var method in handleMethods)
        {
            if (!ReturnsValidCommandType(method))
            {
                violations.Add($"{handlerType.FullName}.{method.Name} - Returns '{method.ReturnType.Name}' instead of Task<Result> or Task<Result<T>>");
            }
        }
    }

    violations.ShouldBeEmpty(ArchitectureTestHelpers.FormatViolations(violations,
        "Command handlers must return Task<Result> or Task<Result<T>>"));
}

private bool ReturnsValidCommandType(MethodInfo method)
{
    var returnType = method.ReturnType;
    
    // Must return Task<T>
    if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
        return false;
    
    var taskGenericArg = returnType.GetGenericArguments()[0];
    
    // Must be Result or Result<T>
    if (taskGenericArg.Name == "Result")
        return true;
        
    if (taskGenericArg.IsGenericType && taskGenericArg.GetGenericTypeDefinition().Name == "Result")
        return true;
    
    return false;
}
```

### Error Messages
- **Violation**: `"Command handler '{HandlerName}.Handle' returns '{ActualReturnType}' - Commands must return Task<Result> or Task<Result<T>>"`
- **Suggested Fix**: `"Change return type to Task<Result> for success/failure indication or Task<Result<T>> to return data"`

---

## Test 6: Queries_ShouldReturn_DTOsNotEntities

### Detection Strategy  
```csharp
[Test]
public void Queries_ShouldReturn_DTOsNotEntities()
{
    var queryHandlerTypes = GetQueryHandlerTypes();
    var violations = new List<string>();

    foreach (var handlerType in queryHandlerTypes)
    {
        var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Handle");

        foreach (var method in handleMethods)
        {
            var returnType = ExtractActualReturnType(method.ReturnType);
            if (returnType != null && IsDomainEntity(returnType))
            {
                violations.Add($"{handlerType.FullName}.{method.Name} - Returns domain entity '{returnType.Name}' instead of DTO");
            }
        }
    }

    violations.ShouldBeEmpty(ArchitectureTestHelpers.FormatViolations(violations,
        "Query handlers must return DTOs, not domain entities"));
}

private Type? ExtractActualReturnType(Type methodReturnType)
{
    // Handle Task<T>
    if (methodReturnType.IsGenericType && methodReturnType.GetGenericTypeDefinition() == typeof(Task<>))
    {
        var taskGenericArg = methodReturnType.GetGenericArguments()[0];
        
        // Handle Result<T>
        if (taskGenericArg.IsGenericType && taskGenericArg.GetGenericTypeDefinition().Name == "Result")
        {
            return taskGenericArg.GetGenericArguments()[0];
        }
        
        return taskGenericArg;
    }
    
    return null;
}

private bool IsDomainEntity(Type type)
{
    // Check if type is in Domain namespace
    if (type.Namespace?.Contains(".Domain.") == true)
        return true;
    
    // Check if type represents a domain concept (entities, aggregates, value objects)
    if (type.Namespace?.Contains(".Entities.") == true || 
        type.Namespace?.Contains(".ValueObjects.") == true ||
        type.Namespace?.Contains(".Aggregates.") == true)
        return true;
    
    return false;
}
```

### Error Messages
- **Violation**: `"Query handler '{HandlerName}.Handle' returns domain entity '{EntityName}' - Queries should return DTOs to prevent domain leakage"`
- **Suggested Fix**: `"Create a DTO/Response class in Application layer and map the domain entity to it before returning"`

---

## Helper Methods Integration

### Extended ArchitectureTestHelpers Methods
```csharp
public static class ArchitectureTestHelpers
{
    // Existing methods...
    
    /// <summary>
    /// Gets all command handler types in the application
    /// </summary>
    public static Type[] GetCommandHandlerTypes()
    {
        return Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .HaveNameEndingWith("Handler")
            .GetTypes();
    }
    
    /// <summary>
    /// Gets all query handler types in the application  
    /// </summary>
    public static Type[] GetQueryHandlerTypes()
    {
        return Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Queries.*")
            .And()
            .HaveNameEndingWith("Handler")
            .GetTypes();
    }
    
    /// <summary>
    /// Gets all command types in the application
    /// </summary>
    public static Type[] GetCommandTypes()
    {
        return Types.InCurrentDomain()
            .That()
            .ResideInNamespace("*.Commands.*")
            .And()
            .HaveNameEndingWith("Command")
            .GetTypes();
    }
}
```

## Performance Considerations

1. **Caching**: Use reflection cache for repeated type analysis
2. **Lazy evaluation**: Only analyze types that match initial filters
3. **Parallel processing**: Where safe, use parallel LINQ for independent validations
4. **Memory efficiency**: Dispose of reflection objects and use lightweight type comparisons

## Integration Timeline

1. **Phase 1**: Implement Commands_ShouldModify_State and Commands_ShouldHave_FluentValidationValidators
2. **Phase 2**: Add query-related validations (Queries_ShouldNever_ModifyState, Queries_ShouldReturn_DTOsNotEntities)  
3. **Phase 3**: Complete with return type validations and validator location tests
4. **Phase 4**: Optimize performance and add comprehensive error messages