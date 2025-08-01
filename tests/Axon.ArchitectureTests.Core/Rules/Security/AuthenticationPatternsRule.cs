using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Security;

/// <summary>
/// Rule to validate authentication patterns and implementations across the application.
/// </summary>
public sealed class AuthenticationPatternsRule : ArchitectureRuleBase
{
    public override string RuleId => "SEC001";
    public override string Name => "Authentication Patterns";
    public override string Description => "Validates that authentication patterns are properly implemented";
    public override string Category => "Security";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            foreach (var type in context.Types.Where(t => !t.IsAbstract && !t.IsInterface))
            {
                // Check for authentication endpoints without authorization
                if (IsAuthenticationEndpoint(type))
                {
                    ValidateAuthenticationEndpoint(type, violations);
                }

                // Check for password handling
                if (HasPasswordHandling(type))
                {
                    ValidatePasswordHandling(type, violations);
                }

                // Check for token handling
                if (HasTokenHandling(type))
                {
                    ValidateTokenHandling(type, violations);
                }

                // Check for authentication attributes
                ValidateAuthenticationAttributes(type, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsAuthenticationEndpoint(Type type)
    {
        return type.Name.Contains("Auth", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Login", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
               type.GetMethods().Any(m => m.Name.Contains("Auth", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasPasswordHandling(Type type)
    {
        return type.GetProperties().Any(p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)) ||
               type.GetFields().Any(f => f.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)) ||
               type.GetMethods().Any(m => m.GetParameters().Any(p => p.Name?.Contains("Password", StringComparison.OrdinalIgnoreCase) == true));
    }

    private static bool HasTokenHandling(Type type)
    {
        return type.GetProperties().Any(p => p.Name.Contains("Token", StringComparison.OrdinalIgnoreCase)) ||
               type.GetFields().Any(f => f.Name.Contains("Token", StringComparison.OrdinalIgnoreCase)) ||
               type.GetMethods().Any(m => m.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    private void ValidateAuthenticationEndpoint(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        
        foreach (var method in methods)
        {
            // Check if authentication endpoints have proper security measures
            var hasAuthorizeAttribute = method.GetCustomAttributes()
                .Any(attr => attr.GetType().Name.Contains("Authorize"));

            var hasAllowAnonymousAttribute = method.GetCustomAttributes()
                .Any(attr => attr.GetType().Name.Contains("AllowAnonymous"));

            if (!hasAuthorizeAttribute && !hasAllowAnonymousAttribute && 
                (method.Name.Contains("Login", StringComparison.OrdinalIgnoreCase) ||
                 method.Name.Contains("Auth", StringComparison.OrdinalIgnoreCase)))
            {
                // Login endpoints should explicitly allow anonymous access
                if (method.Name.Contains("Login", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Authentication endpoint '{method.Name}' should have [AllowAnonymous] attribute",
                        "Add [AllowAnonymous] attribute to login endpoints"));
                }
                else
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Authentication endpoint '{method.Name}' should have security attributes",
                        "Add [Authorize] or [AllowAnonymous] attribute as appropriate"));
                }
            }
        }
    }

    private void ValidatePasswordHandling(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties();
        var fields = type.GetFields();

        // Check password properties
        foreach (var property in properties)
        {
            if (property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase))
            {
                // Passwords should not be exposed in responses
                if (type.Name.Contains("Response", StringComparison.OrdinalIgnoreCase) ||
                    type.Name.Contains("Result", StringComparison.OrdinalIgnoreCase) ||
                    type.Name.Contains("Dto", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Password property '{property.Name}' should not be included in response/DTO types",
                        "Remove password from response types or use [JsonIgnore] attribute"));
                }

                // Check for password validation attributes
                var hasValidationAttributes = property.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name.Contains("Required") ||
                                attr.GetType().Name.Contains("MinLength") ||
                                attr.GetType().Name.Contains("StringLength"));

                if (!hasValidationAttributes && type.Name.Contains("Request", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Password property '{property.Name}' should have validation attributes",
                        "Add [Required], [MinLength], or [StringLength] attributes to password properties"));
                }
            }
        }
    }

    private void ValidateTokenHandling(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            if (method.Name.Contains("Token", StringComparison.OrdinalIgnoreCase))
            {
                // Token generation/validation methods should be secure
                var parameters = method.GetParameters();
                var hasSecureParameters = parameters.Any(p => 
                    p.Name?.Contains("Secret", StringComparison.OrdinalIgnoreCase) == true ||
                    p.Name?.Contains("Key", StringComparison.OrdinalIgnoreCase) == true ||
                    p.ParameterType.Name.Contains("Configuration"));

                if (method.Name.Contains("Generate", StringComparison.OrdinalIgnoreCase) && !hasSecureParameters)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Token generation method '{method.Name}' should use secure parameters",
                        "Use configuration or secure key parameters for token generation"));
                }
            }
        }
    }

    private void ValidateAuthenticationAttributes(Type type, List<RuleViolation> violations)
    {
        // Check if controllers/endpoints have proper authentication attributes
        if (IsControllerOrEndpoint(type))
        {
            var hasAuthorizeAttribute = type.GetCustomAttributes()
                .Any(attr => attr.GetType().Name.Contains("Authorize"));

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var hasPublicEndpoints = methods.Any(m => 
                m.GetCustomAttributes().Any(attr => 
                    attr.GetType().Name.Contains("HttpGet") ||
                    attr.GetType().Name.Contains("HttpPost") ||
                    attr.GetType().Name.Contains("HttpPut") ||
                    attr.GetType().Name.Contains("HttpDelete")));

            if (hasPublicEndpoints && !hasAuthorizeAttribute)
            {
                var hasMethodLevelAuth = methods.Any(m => 
                    m.GetCustomAttributes().Any(attr => 
                        attr.GetType().Name.Contains("Authorize") ||
                        attr.GetType().Name.Contains("AllowAnonymous")));

                if (!hasMethodLevelAuth)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Controller '{type.Name}' has public endpoints but no authentication attributes",
                        "Add [Authorize] at class level or method level authentication attributes"));
                }
            }
        }
    }

    private static bool IsControllerOrEndpoint(Type type)
    {
        return type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase) ||
               type.GetCustomAttributes().Any(attr => 
                   attr.GetType().Name.Contains("Controller") ||
                   attr.GetType().Name.Contains("ApiController") ||
                   attr.GetType().Name.Contains("Route"));
    }
}