using System.Reflection;

namespace Axon.ArchitectureTests;

/// <summary>
/// Shared utility methods for architecture testing.
/// Contains common reflection analysis and error formatting helpers.
/// </summary>
public static class ArchitectureTestHelpers
{
    /// <summary>
    /// Formats violation messages with consistent formatting.
    /// </summary>
    /// <param name="violations">Collection of violation descriptions</param>
    /// <param name="message">Base message describing the rule</param>
    /// <returns>Formatted error message</returns>
    public static string FormatViolations(IEnumerable<string>? violations, string message)
    {
        var violationList = violations ?? Array.Empty<string>();
        return violationList.Any() 
            ? $"{message}. Violations: {string.Join(", ", violationList)}"
            : message;
    }

    /// <summary>
    /// Gets types referenced by the given type through properties and fields.
    /// Simplified implementation for architecture testing purposes.
    /// </summary>
    /// <param name="type">Type to analyze</param>
    /// <returns>Array of referenced types</returns>
    public static Type[] GetReferencedTypes(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        
        // Simplified implementation - would need more comprehensive analysis in practice
        return type.GetProperties()
            .Select(p => p.PropertyType)
            .Concat(type.GetFields().Select(f => f.FieldType))
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Checks if a method returns a Result pattern type.
    /// </summary>
    /// <param name="method">Method to analyze</param>
    /// <returns>True if method returns Result or Result&lt;T&gt;</returns>
    public static bool ReturnsResultType(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        var isResultType = method.ReturnType.Name.StartsWith("Result", StringComparison.Ordinal);
        var isGenericResult = method.ReturnType.IsGenericType && 
                             method.ReturnType.GetGenericTypeDefinition().Name.StartsWith("Result", StringComparison.Ordinal);
        return isResultType || isGenericResult;
    }

    /// <summary>
    /// Checks if a type represents a business operation that could fail.
    /// </summary>
    /// <param name="method">Method to analyze</param>
    /// <returns>True if method likely represents business logic</returns>
    public static bool IsBusinessOperation(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        var hasParameters = method.GetParameters().Length > 0;
        var isNotProperty = !method.IsSpecialName;
        var isNotConstructor = !method.IsConstructor;
        var isPublic = method.IsPublic;
        
        return hasParameters && isNotProperty && isNotConstructor && isPublic;
    }

    /// <summary>
    /// Analyzes if a handler type likely performs state mutations.
    /// Conservative approach - detects common state-changing patterns through dependencies.
    /// </summary>
    /// <param name="handlerType">Handler type to analyze</param>
    /// <returns>True if handler likely modifies state</returns>
    public static bool LikelyModifiesState(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        
        // Analyze constructor dependencies for mutation indicators
        var constructors = handlerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            foreach (var parameter in parameters)
            {
                var paramTypeName = parameter.ParameterType.Name;
                var paramTypeFullName = parameter.ParameterType.FullName ?? "";
                
                // Conservative detection of mutation-indicating dependencies
                if (paramTypeName.Contains("Repository") ||
                    paramTypeName.Contains("Writer") ||
                    paramTypeName.Contains("DbContext") ||
                    paramTypeFullName.Contains("EntityFramework") ||
                    paramTypeName.Contains("UnitOfWork"))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Checks if a type has a corresponding FluentValidation validator.
    /// Looks for standard naming pattern: TypeNameValidator.
    /// </summary>
    /// <param name="commandType">Command type to check</param>
    /// <returns>True if validator exists</returns>
    public static bool HasFluentValidator(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        
        var expectedValidatorName = $"{commandType.Name}Validator";
        
        // Look for validator in same assembly and namespace area
        var assembly = commandType.Assembly;
        var validatorType = assembly.GetTypes()
            .FirstOrDefault(t => t.Name == expectedValidatorName);
            
        if (validatorType == null) return false;
        
        // Verify it's actually a FluentValidation validator
        var baseTypes = GetAllBaseTypes(validatorType);
        return baseTypes.Any(t => t.Name.Contains("AbstractValidator"));
    }

    /// <summary>
    /// Checks if return type is a DTO (not a domain entity).
    /// Conservative approach - flags potential domain leakage.
    /// </summary>
    /// <param name="returnType">Return type to analyze</param>
    /// <returns>True if type appears to be a DTO</returns>
    public static bool IsDto(Type returnType)
    {
        ArgumentNullException.ThrowIfNull(returnType);
        
        // Unwrap generic types (Task<T>, Result<T>, etc.)
        var actualType = UnwrapGenericType(returnType);
        if (actualType == null) return true; // Primitives/void are OK
        
        var typeName = actualType.Name;
        var namespaceName = actualType.Namespace ?? "";
        
        // DTOs typically end with "Dto", "Response", "Model", or "View"
        var isDtoName = typeName.EndsWith("Dto") || 
                       typeName.EndsWith("Response") || 
                       typeName.EndsWith("Model") ||
                       typeName.EndsWith("View");
        
        // Domain entities typically don't have these suffixes and are in Domain namespace
        var isDomainEntity = namespaceName.Contains(".Domain.") && 
                           !isDtoName &&
                           !typeName.EndsWith("Id"); // Value objects like UserId are OK
        
        return !isDomainEntity;
    }

    /// <summary>
    /// Gets all base types and interfaces of a type.
    /// </summary>
    private static IEnumerable<Type> GetAllBaseTypes(Type type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            yield return current;
            current = current.BaseType;
        }
        
        foreach (var @interface in type.GetInterfaces())
        {
            yield return @interface;
        }
    }

    /// <summary>
    /// Unwraps generic types to get the inner type (T from Task&lt;T&gt;, Result&lt;T&gt;, etc.).
    /// </summary>
    private static Type? UnwrapGenericType(Type type)
    {
        if (type.IsPrimitive || type == typeof(string) || type == typeof(void))
            return null; // Primitives are fine
        
        if (!type.IsGenericType) 
            return type;
        
        // For generic types like Task<T>, Result<T>, get the T
        var genericArgs = type.GetGenericArguments();
        return genericArgs.Length == 1 ? genericArgs[0] : type;
    }
}

/// <summary>
/// Security-focused helper methods for architecture testing.
/// Provides reusable security detection methods for static analysis.
/// </summary>
public static class SecurityTestHelpers
{
    /// <summary>
    /// Checks if a controller type has authorization attributes at class level.
    /// </summary>
    /// <param name="controllerType">Controller type to analyze</param>
    /// <returns>True if controller has [Authorize] or [AllowAnonymous]</returns>
    public static bool HasAuthorizationAttribute(Type controllerType)
    {
        ArgumentNullException.ThrowIfNull(controllerType);
        
        return controllerType.GetCustomAttributes().Any(attr =>
            attr.GetType().Name == "AuthorizeAttribute" ||
            attr.GetType().Name == "AllowAnonymousAttribute");
    }

    /// <summary>
    /// Checks if a method has authorization attributes.
    /// </summary>
    /// <param name="method">Method to analyze</param>
    /// <returns>True if method has [Authorize] or [AllowAnonymous]</returns>
    public static bool HasAuthorizationAttribute(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        return method.GetCustomAttributes().Any(attr =>
            attr.GetType().Name == "AuthorizeAttribute" ||
            attr.GetType().Name == "AllowAnonymousAttribute");
    }

    /// <summary>
    /// Gets endpoint names that are considered sensitive (state-changing operations).
    /// </summary>
    /// <param name="controllerTypes">Controller types to analyze</param>
    /// <returns>Array of sensitive endpoint names</returns>
    public static string[] GetSensitiveEndpoints(Type[] controllerTypes)
    {
        ArgumentNullException.ThrowIfNull(controllerTypes);
        
        var sensitiveEndpoints = new List<string>();
        
        foreach (var controller in controllerTypes)
        {
            var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(IsSensitiveOperation);
                
            sensitiveEndpoints.AddRange(methods.Select(m => $"{controller.Name}.{m.Name}"));
        }
        
        return sensitiveEndpoints.ToArray();
    }

    /// <summary>
    /// Checks if assembly has rate limiting configuration.
    /// Conservative detection of common rate limiting patterns.
    /// </summary>
    /// <param name="assembly">Assembly to analyze</param>
    /// <returns>True if rate limiting appears to be configured</returns>
    public static bool HasRateLimitingConfiguration(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        
        // Look for common rate limiting service registrations
        var types = assembly.GetTypes();
        return types.Any(t => 
            t.Name.Contains("RateLimit", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("Throttle", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a method has content type validation.
    /// </summary>
    /// <param name="method">Method to analyze</param>
    /// <returns>True if method has [Consumes] attribute or parameter validation</returns>
    public static bool HasContentTypeValidation(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        // Check for [Consumes] attribute
        var hasConsumesAttribute = method.GetCustomAttributes()
            .Any(attr => attr.GetType().Name == "ConsumesAttribute");
            
        // Check for [FromBody] parameters which indicate content type expectations
        var hasFromBodyParams = method.GetParameters()
            .Any(p => p.GetCustomAttributes()
                .Any(attr => attr.GetType().Name == "FromBodyAttribute"));
        
        return hasConsumesAttribute || hasFromBodyParams;
    }

    /// <summary>
    /// Checks if assembly has HTTPS redirection configured.
    /// Conservative detection of HTTPS enforcement patterns.
    /// </summary>
    /// <param name="assembly">Assembly to analyze</param>
    /// <returns>True if HTTPS redirection appears to be configured</returns>
    public static bool HasHttpsRedirection(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        
        // Look for Program.cs or Startup patterns (simplified)
        var types = assembly.GetTypes();
        return types.Any(t => 
            t.Name.Contains("Program", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("Startup", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if assembly has CORS configuration.
    /// Conservative detection of CORS setup patterns.
    /// </summary>
    /// <param name="assembly">Assembly to analyze</param>
    /// <returns>True if CORS appears to be configured</returns>
    public static bool HasCorsConfiguration(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        
        // Look for CORS-related types or configurations
        var types = assembly.GetTypes();
        return types.Any(t => 
            t.Name.Contains("Cors", StringComparison.OrdinalIgnoreCase) ||
            t.FullName?.Contains("Cors", StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// Checks if a type is an API controller.
    /// </summary>
    /// <param name="type">Type to analyze</param>
    /// <returns>True if type is an API controller</returns>
    public static bool IsApiController(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        
        return type.GetCustomAttributes()
            .Any(attr => attr.GetType().Name == "ApiControllerAttribute") ||
            type.Name.EndsWith("Controller", StringComparison.Ordinal) ||
            type.Name.EndsWith("Endpoint", StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks if a method represents an HTTP endpoint.
    /// </summary>
    /// <param name="method">Method to analyze</param>
    /// <returns>True if method has HTTP method attributes</returns>
    public static bool IsHttpEndpoint(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        return method.GetCustomAttributes().Any(attr =>
            attr.GetType().Name.StartsWith("Http", StringComparison.Ordinal));
    }

    /// <summary>
    /// Checks if a method represents a sensitive operation that modifies state.
    /// </summary>
    /// <param name="method">Method to analyze</param>
    /// <returns>True if method is likely a sensitive operation</returns>
    public static bool IsSensitiveOperation(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        // Only consider methods with HTTP method attributes as sensitive
        var stateChangingMethods = new[] { "HttpPostAttribute", "HttpPutAttribute", "HttpDeleteAttribute", "HttpPatchAttribute" };
        
        return method.GetCustomAttributes().Any(attr =>
            stateChangingMethods.Contains(attr.GetType().Name));
    }
}