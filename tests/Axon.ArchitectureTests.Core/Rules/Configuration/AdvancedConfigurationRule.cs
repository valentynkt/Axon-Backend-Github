using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Configuration;

/// <summary>
/// Validates advanced configuration patterns and security.
/// </summary>
public sealed class AdvancedConfigurationRule : ArchitectureRuleBase
{
    public override string RuleId => "CONFIG-ADV-001";
    public override string Name => "Advanced Configuration Patterns";
    public override string Description => "Validates advanced configuration patterns including environment-specific settings and secure configuration";
    public override string Category => "Configuration";
    public override RuleSeverity Severity => RuleSeverity.Error;

    private static readonly string[] SensitiveConfigKeys = 
    {
        "password", "secret", "key", "token", "connectionstring", "apikey", "credential"
    };

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {   
        var violations = new List<RuleViolation>();
        var types = context.Types.ToList();

        // Check for secure configuration handling
        await ValidateSecureConfiguration(types, violations);
        
        // Check for environment-specific configuration
        await ValidateEnvironmentConfiguration(types, violations);
        
        // Check for configuration validation
        await ValidateConfigurationValidation(types, violations);
        
        // Check for configuration change detection
        await ValidateConfigurationChangeDetection(types, violations);
        
        return violations;
    }

    private static async Task ValidateSecureConfiguration(List<Type> types, List<RuleViolation> violations)
    {
        var configurationTypes = types.Where(t => 
            t.Name.EndsWith("Options") ||
            t.Name.EndsWith("Configuration") ||
            t.Name.EndsWith("Settings"))
            .ToList();

        foreach (var configType in configurationTypes)
        {
            var properties = configType.GetProperties();
            
            foreach (var property in properties)
            {
                var propertyName = property.Name.ToLower();
                
                // Check for sensitive configuration properties
                if (SensitiveConfigKeys.Any(key => propertyName.Contains(key)))
                {
                    var hasProtectionAttribute = property.GetCustomAttributes()
                        .Any(a => 
                            a.GetType().Name.Contains("Protected") ||
                            a.GetType().Name.Contains("Encrypted") ||
                            a.GetType().Name.Contains("Secure"));

                    if (!hasProtectionAttribute)
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = configType.FullName!,
                            AssemblyName = configType.Assembly.FullName!,
                            Message = $"Sensitive configuration property '{property.Name}' in '{configType.Name}' lacks protection attributes",
                            Severity = RuleSeverity.Error
                        });
                    }

                    // Check if sensitive data is stored as plain string
                    if (property.PropertyType == typeof(string))
                    {
                        violations.Add(new RuleViolation
                        {
                            TypeName = configType.FullName!,
                            AssemblyName = configType.Assembly.FullName!,
                            Message = $"Sensitive configuration property '{property.Name}' in '{configType.Name}' should use SecureString or encrypted type",
                            Severity = RuleSeverity.Error
                        });
                    }
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateEnvironmentConfiguration(List<Type> types, List<RuleViolation> violations)
    {
        var configurationTypes = types.Where(t => 
            t.Name.EndsWith("Options") ||
            t.Name.EndsWith("Configuration") ||
            t.Name.EndsWith("Settings"))
            .ToList();

        foreach (var configType in configurationTypes)
        {
            var hasEnvironmentBinding = configType.GetCustomAttributes()
                .Any(a => 
                    a.GetType().Name.Contains("ConfigurationSection") ||
                    a.GetType().Name.Contains("Bind") ||
                    a.GetType().Name.Contains("Options"));

            if (!hasEnvironmentBinding)
            {
                violations.Add(new RuleViolation
                {
                    TypeName = configType.FullName!,
                    AssemblyName = configType.Assembly.FullName!,
                    Message = $"Configuration type '{configType.Name}' lacks environment-specific binding attributes",
                    Severity = RuleSeverity.Error
                });
            }

            // Check for environment-specific properties
            var properties = configType.GetProperties();
            var hasEnvironmentProperties = properties.Any(p => 
                p.Name.ToLower().Contains("environment") ||
                p.Name.ToLower().Contains("env"));

            if (!hasEnvironmentProperties && configType.Name.Contains("App"))
            {
                violations.Add(new RuleViolation
                {
                    TypeName = configType.FullName!,
                    AssemblyName = configType.Assembly.FullName!,
                    Message = $"Application configuration type '{configType.Name}' should include environment identification",
                    Severity = RuleSeverity.Error
                });
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateConfigurationValidation(List<Type> types, List<RuleViolation> violations)
    {
        var configurationTypes = types.Where(t => 
            t.Name.EndsWith("Options") ||
            t.Name.EndsWith("Configuration") ||
            t.Name.EndsWith("Settings"))
            .ToList();

        foreach (var configType in configurationTypes)
        {
            var implementsValidation = configType.GetInterfaces()
                .Any(i => i.Name.Contains("IValidatable") ||
                        i.Name.Contains("IPostConfigureOptions"));

            if (!implementsValidation)
            {
                violations.Add(new RuleViolation
                {
                    TypeName = configType.FullName!,
                    AssemblyName = configType.Assembly.FullName!,
                    Message = $"Configuration type '{configType.Name}' should implement validation interface",
                    Severity = RuleSeverity.Error
                });
            }

            // Check for validation attributes on properties
            var properties = configType.GetProperties();
            foreach (var property in properties)
            {
                var hasValidationAttributes = property.GetCustomAttributes()
                    .Any(a => 
                        a.GetType().Name.Contains("Required") ||
                        a.GetType().Name.Contains("Range") ||
                        a.GetType().Name.Contains("MinLength") ||
                        a.GetType().Name.Contains("MaxLength"));

                // Critical properties should have validation
                var isCriticalProperty = property.Name.ToLower().Contains("url") ||
                                       property.Name.ToLower().Contains("connection") ||
                                       property.Name.ToLower().Contains("timeout");

                if (isCriticalProperty && !hasValidationAttributes)
                {
                    violations.Add(new RuleViolation
                    {
                        TypeName = configType.FullName!,
                        AssemblyName = configType.Assembly.FullName!,
                        Message = $"Critical configuration property '{property.Name}' in '{configType.Name}' lacks validation attributes",
                        Severity = RuleSeverity.Error
                    });
                }
            }
        }
        
        await Task.CompletedTask;
    }

    private static async Task ValidateConfigurationChangeDetection(List<Type> types, List<RuleViolation> violations)
    {
        var configurationServiceTypes = types.Where(t => 
            t.Name.Contains("Configuration") && t.Name.Contains("Service"))
            .ToList();

        foreach (var serviceType in configurationServiceTypes)
        {
            var hasChangeDetection = serviceType.GetMethods()
                .Any(m => m.Name.Contains("OnConfigurationChanged") ||
                        m.Name.Contains("OnChange") ||
                        m.Name.Contains("Reload"));

            var implementsChangeNotification = serviceType.GetInterfaces()
                .Any(i => i.Name.Contains("IOptionsMonitor") ||
                        i.Name.Contains("IChangeToken"));

            if (!hasChangeDetection && !implementsChangeNotification)
            {
                violations.Add(new RuleViolation
                {
                    TypeName = serviceType.FullName!,
                    AssemblyName = serviceType.Assembly.FullName!,
                    Message = $"Configuration service '{serviceType.Name}' should support configuration change detection",
                    Severity = RuleSeverity.Error
                });
            }
        }
        
        await Task.CompletedTask;
    }
}