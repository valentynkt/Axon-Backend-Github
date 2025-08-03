using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Security;

/// <summary>
/// Rule to validate authorization patterns and implementations across the application.
/// </summary>
public sealed class AuthorizationPatternsRule : ArchitectureRuleBase
{
    public override string RuleId => "SEC002";
    public override string Name => "Authorization Patterns";
    public override string Description => "Validates that authorization patterns are properly implemented";
    public override string Category => "Security";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(IArchitectureContext context, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            foreach (var assembly in context.Assemblies)
            {
                // Skip system assemblies if they somehow got through
                if (IsSystemAssembly(assembly))
                    continue;
                var types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface);

                foreach (var type in types)
                {
                    // Skip architecture test framework types
                    if (type.Namespace?.Contains("ArchitectureTests", StringComparison.OrdinalIgnoreCase) == true)
                        continue;
                    // Check authorization attributes on endpoints
                    if (IsApiEndpoint(type))
                    {
                        ValidateEndpointAuthorization(type, violations);
                    }

                    // Check for role-based authorization
                    if (HasRoleBasedFeatures(type))
                    {
                        ValidateRoleBasedAuthorization(type, violations);
                    }

                    // Check for policy-based authorization
                    ValidatePolicyBasedAuthorization(type, violations);

                    // Check for resource-based authorization
                    ValidateResourceBasedAuthorization(type, violations);
                }
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsApiEndpoint(Type type)
    {
        return type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase) ||
               type.GetCustomAttributes().Any(attr => 
                   attr.GetType().Name.Contains("Controller") ||
                   attr.GetType().Name.Contains("ApiController"));
    }

    private static bool HasRoleBasedFeatures(Type type)
    {
        return type.GetMethods().Any(m => 
            m.Name.Contains("Role", StringComparison.OrdinalIgnoreCase) ||
            m.GetParameters().Any(p => p.Name?.Contains("Role", StringComparison.OrdinalIgnoreCase) == true)) ||
               type.GetProperties().Any(p => p.Name.Contains("Role", StringComparison.OrdinalIgnoreCase));
    }

    private void ValidateEndpointAuthorization(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        
        var classHasAuthorize = type.GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("Authorize"));

        foreach (var method in methods)
        {
            var isHttpEndpoint = method.GetCustomAttributes()
                .Any(attr => attr.GetType().Name.StartsWith("Http", StringComparison.OrdinalIgnoreCase));

            if (isHttpEndpoint)
            {
                var hasAuthorize = method.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name.Contains("Authorize"));

                var hasAllowAnonymous = method.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name.Contains("AllowAnonymous"));

                // Check if endpoint needs authorization
                if (!classHasAuthorize && !hasAuthorize && !hasAllowAnonymous)
                {
                    // Determine if this should be a public endpoint
                    var shouldBePublic = IsPublicEndpoint(method);
                    
                    if (!shouldBePublic)
                    {
                        violations.Add(CreateViolation(type,
                            $"Endpoint '{method.Name}' lacks authorization attributes",
                            "Add [Authorize] attribute or explicitly mark as [AllowAnonymous]"));
                    }
                }

                // Check for overly permissive authorization
                if (hasAuthorize)
                {
                    ValidateAuthorizationSpecificity(type, method, violations);
                }
            }
        }
    }

    private static bool IsPublicEndpoint(MethodInfo method)
    {
        // Common patterns for endpoints that should be public
        var publicPatterns = new[]
        {
            "login", "register", "signin", "signup", "token", "refresh",
            "health", "ping", "status", "version", "swagger", "openapi"
        };

        return publicPatterns.Any(pattern => 
            method.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private void ValidateAuthorizationSpecificity(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        var authorizeAttributes = method.GetCustomAttributes()
            .Where(attr => attr.GetType().Name.Contains("Authorize"))
            .ToList();

        foreach (var attribute in authorizeAttributes)
        {
            // Check if authorization is too generic (no roles, policies, or schemes specified)
            var attributeType = attribute.GetType();
            
            // Use reflection to check for Roles, Policy, or AuthenticationSchemes properties
            var hasRoles = HasPropertyWithValue(attribute, "Roles");
            var hasPolicy = HasPropertyWithValue(attribute, "Policy");
            var hasSchemes = HasPropertyWithValue(attribute, "AuthenticationSchemes");

            if (!hasRoles && !hasPolicy && !hasSchemes)
            {
                // For sensitive operations, require more specific authorization
                if (IsSensitiveOperation(method))
                {
                    violations.Add(CreateViolation(type,
                        $"Sensitive endpoint '{method.Name}' uses generic [Authorize] without specific roles or policies",
                        "Use role-based or policy-based authorization for sensitive operations"));
                }
            }
        }
    }

    private static bool HasPropertyWithValue(object attribute, string propertyName)
    {
        try
        {
            var property = attribute.GetType().GetProperty(propertyName);
            if (property != null)
            {
                var value = property.GetValue(attribute);
                return value != null && !string.IsNullOrEmpty(value.ToString());
            }
        }
        catch (ArgumentException)
        {
            // Ignore property access errors
        }
        catch (TargetParameterCountException)
        {
            // Ignore parameter count errors
        }
        return false;
    }

    private static bool IsSensitiveOperation(MethodInfo method)
    {
        var sensitivePatterns = new[]
        {
            "delete", "remove", "admin", "manage", "create", "update", 
            "modify", "change", "reset", "approve", "reject"
        };

        return sensitivePatterns.Any(pattern => 
            method.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private void ValidateRoleBasedAuthorization(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            if (method.Name.Contains("Role", StringComparison.OrdinalIgnoreCase))
            {
                // Methods dealing with roles should have proper authorization
                var hasRoleBasedAuth = method.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name.Contains("Authorize") && 
                                HasPropertyWithValue(attr, "Roles"));

                if (!hasRoleBasedAuth)
                {
                    violations.Add(CreateViolation(type,
                        $"Role management method '{method.Name}' should use role-based authorization",
                        "Add [Authorize(Roles = \"AdminRole\")] or similar role-based authorization"));
                }
            }
        }
    }

    private void ValidatePolicyBasedAuthorization(Type type, List<RuleViolation> violations)
    {
        // Check for complex authorization scenarios that should use policies
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            var authorizeAttributes = method.GetCustomAttributes()
                .Where(attr => attr.GetType().Name.Contains("Authorize"))
                .ToList();

            foreach (var attribute in authorizeAttributes)
            {
                var hasMultipleRoles = HasPropertyWithValue(attribute, "Roles") && 
                    GetPropertyValue(attribute, "Roles")?.Contains(',') == true;

                if (hasMultipleRoles)
                {
                    violations.Add(CreateViolation(type,
                        $"Method '{method.Name}' uses complex role authorization that should be a policy",
                        "Create an authorization policy instead of using multiple roles"));
                }
            }
        }
    }

    private void ValidateResourceBasedAuthorization(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            // Check for methods that should use resource-based authorization
            var hasIdParameter = method.GetParameters()
                .Any(p => p.Name?.Contains("Id", StringComparison.OrdinalIgnoreCase) == true);

            var isResourceOperation = IsSensitiveOperation(method) && hasIdParameter;

            if (isResourceOperation)
            {
                var hasResourceAuth = method.GetParameters()
                    .Any(p => p.ParameterType.Name.Contains("IAuthorizationService")) ||
                    method.GetCustomAttributes()
                    .Any(attr => HasPropertyWithValue(attr, "Policy"));

                if (!hasResourceAuth)
                {
                    violations.Add(CreateViolation(type,
                        $"Resource operation '{method.Name}' should implement resource-based authorization",
                        "Use IAuthorizationService or resource-specific policies for operations on specific resources"));
                }
            }
        }
    }

    private static string? GetPropertyValue(object attribute, string propertyName)
    {
        try
        {
            var property = attribute.GetType().GetProperty(propertyName);
            return property?.GetValue(attribute)?.ToString();
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (TargetParameterCountException)
        {
            return null;
        }
    }

    
    private static bool IsSystemAssembly(Assembly assembly)
    {
        var name = assembly.FullName ?? "";
        return name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase);
    }
}