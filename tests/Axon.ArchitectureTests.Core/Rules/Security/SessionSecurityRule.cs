using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Security;

/// <summary>
/// Validates session security patterns and implementation.
/// </summary>
public sealed class SessionSecurityRule : ArchitectureRuleBase
{
    public override string RuleId => "SEC-SESSION-001";
    public override string Name => "Session Security";
    public override string Description => "Validates secure session management patterns";
    public override string Category => "Security";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {   
        var violations = new List<RuleViolation>();
        var types = context.Types.ToList();

        // Check for secure session configuration
        await ValidateSessionConfiguration(types, violations);
        
        // Check for session timeout handling
        await ValidateSessionTimeouts(types, violations);
        
        // Check for session regeneration on privilege elevation
        await ValidateSessionRegeneration(types, violations);
        
        return violations;
    }

    private static async Task ValidateSessionConfiguration(List<Type> types, List<RuleViolation> violations)
    {
        var configurationTypes = types.Where(t => 
            t.Name.Contains("Configuration") ||
            t.Name.Contains("Options") ||
            t.Name.Contains("Settings"))
            .ToList();

        foreach (var configType in configurationTypes)
        {
            var sessionProperties = configType.GetProperties()
                .Where(p => p.Name.ToLower().Contains("session"))
                .ToList();

            foreach (var property in sessionProperties)
            {
                // Check for secure session attributes
                var hasSecureAttributes = property.GetCustomAttributes()
                    .Any(a => 
                        a.GetType().Name.Contains("Secure") ||
                        a.GetType().Name.Contains("HttpOnly") ||
                        a.GetType().Name.Contains("SameSite"));

                if (!hasSecureAttributes && property.Name.ToLower().Contains("cookie"))
                {
                    violations.Add(new RuleViolation
                    {
                        Message = $"Session property '{property.Name}' in '{configType.Name}' lacks secure cookie attributes",
                        TypeName = configType.FullName!,
                        AssemblyName = configType.Assembly.GetName().Name ?? "Unknown",
                        Severity = RuleSeverity.Error
                    });
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateSessionTimeouts(List<Type> types, List<RuleViolation> violations)
    {
        var authenticationTypes = types.Where(t => 
            t.Name.Contains("Authentication") ||
            t.Name.Contains("Session") ||
            t.Name.Contains("Login"))
            .ToList();

        foreach (var authType in authenticationTypes)
        {
            var methods = authType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var method in methods)
            {
                if (method.Name.ToLower().Contains("authenticate") || 
                    method.Name.ToLower().Contains("login"))
                {
                    // Check for timeout validation
                    var hasTimeoutHandling = method.GetParameters()
                        .Any(p => p.Name?.ToLower().Contains("timeout") == true ||
                                p.Name?.ToLower().Contains("expiry") == true);

                    if (!hasTimeoutHandling)
                    {
                        violations.Add(new RuleViolation
                        {
                            Message = $"Authentication method '{method.Name}' in '{authType.Name}' lacks timeout handling",
                            TypeName = authType.FullName!,
                            AssemblyName = authType.Assembly.GetName().Name ?? "Unknown",
                            Severity = RuleSeverity.Error
                        });
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateSessionRegeneration(List<Type> types, List<RuleViolation> violations)
    {
        var authenticationTypes = types.Where(t => 
            t.Name.Contains("Authentication") ||
            t.Name.Contains("Authorization") ||
            t.Name.Contains("Login"))
            .ToList();

        foreach (var authType in authenticationTypes)
        {
            var methods = authType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            
            var privilegeElevationMethods = methods.Where(m => 
                m.Name.ToLower().Contains("elevate") ||
                m.Name.ToLower().Contains("promote") ||
                m.Name.ToLower().Contains("admin"))
                .ToList();

            foreach (var method in privilegeElevationMethods)
            {
                var hasSessionRegeneration = method.GetCustomAttributes()
                    .Any(a => a.GetType().Name.Contains("RegenerateSession")) ||
                    method.Name.ToLower().Contains("regenerate");

                if (!hasSessionRegeneration)
                {
                    violations.Add(new RuleViolation
                    {
                        Message = $"Privilege elevation method '{method.Name}' in '{authType.Name}' should regenerate session",
                        TypeName = authType.FullName!,
                        AssemblyName = authType.Assembly.GetName().Name ?? "Unknown",
                        Severity = RuleSeverity.Error
                    });
                }
            }
        }
        
        await Task.CompletedTask;
    }
}