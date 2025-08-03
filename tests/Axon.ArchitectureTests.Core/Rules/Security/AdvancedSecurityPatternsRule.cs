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

    private static async Task ValidateCsrfProtection(List<Type> types, List<RuleViolation> violations)
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
                    violations.Add(new RuleViolation
                    {
                        TypeName = controller.FullName!,
                        AssemblyName = controller.Assembly.FullName!,
                        Message = $"Method '{method.Name}' in '{controller.Name}' lacks anti-forgery token validation",
                        Severity = RuleSeverity.Critical
                    });
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateXssProtection(List<Type> types, List<RuleViolation> violations)
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
                    violations.Add(new RuleViolation
                    {
                        TypeName = viewModel.FullName!,
                        AssemblyName = viewModel.Assembly.FullName!,
                        Message = $"Property '{property.Name}' in '{viewModel.Name}' may be vulnerable to XSS attacks",
                        Severity = RuleSeverity.Critical
                    });
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateSecureDataHandling(List<Type> types, List<RuleViolation> violations)
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
                        violations.Add(new RuleViolation
                        {
                            TypeName = type.FullName!,
                            AssemblyName = type.Assembly.FullName!,
                            Message = $"Sensitive property '{property.Name}' in '{type.Name}' lacks data protection",
                            Severity = RuleSeverity.Critical
                        });
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateRateLimiting(List<Type> types, List<RuleViolation> violations)
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
                    violations.Add(new RuleViolation
                    {
                        TypeName = apiType.FullName!,
                        AssemblyName = apiType.Assembly.FullName!,
                        Message = $"API type '{apiType.Name}' lacks rate limiting protection",
                        Severity = RuleSeverity.Critical
                    });
                }
            }
        }
        
        await Task.CompletedTask;
    }
}