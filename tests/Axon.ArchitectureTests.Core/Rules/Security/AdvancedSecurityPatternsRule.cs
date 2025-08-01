using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Security;

/// <summary>
/// Validates advanced security patterns for comprehensive application security.
/// </summary>
public sealed class AdvancedSecurityPatternsRule : ArchitectureRuleBase
{
    public override string RuleId => "SEC-ADV-001";
    public override string Name => "Advanced Security Patterns";
    public override string Description => "Validates comprehensive security patterns including CSRF protection, XSS prevention, and secure data handling";
    public override string Category => "Security";
    public override RuleSeverity Severity => RuleSeverity.Critical;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {   
        var violations = new List<RuleViolation>();
        var types = context.Types.ToList();

        // Check for CSRF protection in controllers
        await ValidateCsrfProtection(types, violations);
        
        // Check for XSS prevention patterns
        await ValidateXssProtection(types, violations);
        
        // Check for secure data handling
        await ValidateSecureDataHandling(types, violations);
        
        // Check for rate limiting implementations
        await ValidateRateLimiting(types, violations);
        
        return violations;
    }

    private async Task ValidateCsrfProtection(List<Type> types, List<RuleViolation> violations)
    {   
        var controllerTypes = types.Where(t => 
            t.Name.EndsWith("Controller") || 
            t.Name.EndsWith("Endpoint") ||
            t.GetCustomAttributes().Any(a => a.GetType().Name.Contains("Controller")))
            .ToList();

        foreach (var controller in controllerTypes)
        {   
            var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == controller)
                .ToList();

            var postMethods = methods.Where(m => 
                m.GetCustomAttributes().Any(a => 
                    a.GetType().Name.Contains("Post") ||
                    a.GetType().Name.Contains("Put") ||
                    a.GetType().Name.Contains("Delete")))
                .ToList();

            foreach (var method in postMethods)
            {   
                var hasAntiForgeryToken = method.GetCustomAttributes()
                    .Any(a => a.GetType().Name.Contains("ValidateAntiForgeryToken"));

                if (!hasAntiForgeryToken)
                {   
                    violations.Add(new RuleViolation(
                        $"Method '{method.Name}' in '{controller.Name}' lacks anti-forgery token validation",
                        controller.FullName!,
                        method.Name));
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private async Task ValidateXssProtection(List<Type> types, List<RuleViolation> violations)
    {   
        var viewModelTypes = types.Where(t => 
            t.Name.EndsWith("ViewModel") ||
            t.Name.EndsWith("Response") ||
            t.Name.EndsWith("Dto"))
            .ToList();

        foreach (var viewModel in viewModelTypes)
        {   
            var stringProperties = viewModel.GetProperties()
                .Where(p => p.PropertyType == typeof(string))
                .ToList();

            foreach (var property in stringProperties)
            {   
                var hasHtmlEncoding = property.GetCustomAttributes()
                    .Any(a => 
                        a.GetType().Name.Contains("AllowHtml") ||
                        a.GetType().Name.Contains("Raw") ||
                        a.GetType().Name.Contains("Encoded"));

                // Check if property name suggests user input
                var isUserInput = property.Name.ToLower().Contains("content") ||
                                property.Name.ToLower().Contains("message") ||
                                property.Name.ToLower().Contains("description") ||
                                property.Name.ToLower().Contains("comment");

                if (isUserInput && !hasHtmlEncoding)
                {   
                    violations.Add(new RuleViolation(
                        $"Property '{property.Name}' in '{viewModel.Name}' may be vulnerable to XSS attacks",
                        viewModel.FullName!,
                        property.Name));
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private async Task ValidateSecureDataHandling(List<Type> types, List<RuleViolation> violations)
    {   
        foreach (var type in types)
        {   
            var properties = type.GetProperties();
            
            foreach (var property in properties)
            {   
                // Check for sensitive data without proper protection
                var isSensitive = property.Name.ToLower().Contains("password") ||
                                property.Name.ToLower().Contains("secret") ||
                                property.Name.ToLower().Contains("token") ||
                                property.Name.ToLower().Contains("key");

                if (isSensitive)
                {   
                    var hasDataProtection = property.GetCustomAttributes()
                        .Any(a => 
                            a.GetType().Name.Contains("DataProtection") ||
                            a.GetType().Name.Contains("Encrypted") ||
                            a.GetType().Name.Contains("Protected"));

                    if (!hasDataProtection && property.PropertyType == typeof(string))
                    {   
                        violations.Add(new RuleViolation(
                            $"Sensitive property '{property.Name}' in '{type.Name}' lacks data protection",
                            type.FullName!,
                            property.Name));
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private async Task ValidateRateLimiting(List<Type> types, List<RuleViolation> violations)
    {   
        var apiTypes = types.Where(t => 
            t.Name.EndsWith("Controller") ||
            t.Name.EndsWith("Endpoint") ||
            t.Namespace?.Contains("Api") == true)
            .ToList();

        foreach (var apiType in apiTypes)
        {   
            var hasRateLimiting = apiType.GetCustomAttributes()
                .Any(a => 
                    a.GetType().Name.Contains("RateLimit") ||
                    a.GetType().Name.Contains("Throttle"));

            var publicMethods = apiType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == apiType)
                .Where(m => m.GetCustomAttributes()
                    .Any(a => a.GetType().Name.Contains("Http")))
                .ToList();

            if (publicMethods.Any() && !hasRateLimiting)
            {   
                var hasMethodLevelRateLimit = publicMethods.Any(m => 
                    m.GetCustomAttributes()
                        .Any(a => 
                            a.GetType().Name.Contains("RateLimit") ||
                            a.GetType().Name.Contains("Throttle")));
                        
                if (!hasMethodLevelRateLimit)
                {   
                    violations.Add(new RuleViolation(
                        $"API type '{apiType.Name}' lacks rate limiting protection",
                        apiType.FullName!,
                        "Class"));
                }
            }
        }
        
        await Task.CompletedTask;
    }
}