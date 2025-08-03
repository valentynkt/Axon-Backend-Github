using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Configuration;

/// <summary>
/// Rule to validate proper IOptions and configuration patterns.
/// </summary>
public sealed class AppSettingsRule : ArchitectureRuleBase
{
    public override string RuleId => "CONFIG_001";
    public override string Name => "App Settings Pattern";
    public override string Description => "Validates that IOptions<T> usage follows proper configuration patterns";
    public override string Category => "Configuration";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            foreach (var assembly in context.Assemblies)
            {
                var types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface);

                foreach (var type in types)
                {
                    // Skip architecture test framework types - they don't need application configuration
                    if (type.Namespace?.Contains("ArchitectureTests", StringComparison.OrdinalIgnoreCase) == true)
                        continue;
                        
                    ValidateOptionsUsage(type, violations);
                    ValidateConfigurationBinding(type, violations);
                    ValidateSettingsClasses(type, violations);
                }
            }
        }, cancellationToken);

        return violations;
    }

    private void ValidateOptionsUsage(Type type, List<RuleViolation> violations)
    {
        var constructors = type.GetConstructors();
        
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            
            foreach (var parameter in parameters)
            {
                // Check for IConfiguration usage in services (should prefer IOptions<T>)
                if (parameter.ParameterType.Name == "IConfiguration" && 
                    !IsControllerOrMinimalApi(type))
                {
                    violations.Add(CreateViolation(type,
                        $"Constructor parameter '{parameter.Name}' uses IConfiguration directly. Consider using IOptions<T> for strongly-typed configuration",
                        "Use IOptions<T> or IOptionsSnapshot<T> for strongly-typed configuration access"));
                }

                // Check for proper IOptions<T> usage
                if (parameter.ParameterType.Name.StartsWith("IOptions"))
                {
                    var genericArgs = parameter.ParameterType.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        var optionsType = genericArgs[0];
                        if (!IsValidOptionsType(optionsType))
                        {
                            violations.Add(CreateViolation(type,
                                $"IOptions<{optionsType.Name}> uses invalid options type. Options classes should be simple POCOs",
                                "Create a dedicated configuration class with public properties"));
                        }
                    }
                }
            }
        }
    }

    private void ValidateConfigurationBinding(Type type, List<RuleViolation> violations)
    {
        if (IsConfigurationOptionsType(type))
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                // Configuration properties should have public getters and setters
                if (!property.CanRead || !property.CanWrite)
                {
                    violations.Add(CreateViolation(type,
                        $"Configuration property '{property.Name}' should have both getter and setter for proper binding",
                        "Add public getter and setter to configuration properties"));
                }

                // Check for parameterless constructor requirement
                if (property.PropertyType.IsClass && 
                    property.PropertyType != typeof(string) &&
                    !property.PropertyType.IsArray)
                {
                    var hasParameterlessConstructor = property.PropertyType
                        .GetConstructors()
                        .Any(c => c.GetParameters().Length == 0);

                    if (!hasParameterlessConstructor)
                    {
                        violations.Add(CreateViolation(type,
                            $"Configuration property '{property.Name}' of type '{property.PropertyType.Name}' should have parameterless constructor",
                            "Add parameterless constructor to nested configuration types"));
                    }
                }
            }
        }
    }

    private void ValidateSettingsClasses(Type type, List<RuleViolation> violations)
    {
        if (IsConfigurationOptionsType(type))
        {
            // Settings classes should be sealed or have virtual members appropriately
            if (!type.IsSealed && type.GetProperties().Any(p => p.GetSetMethod()?.IsVirtual == true))
            {
                violations.Add(CreateViolation(type,
                    $"Configuration class '{type.Name}' has virtual properties but is not sealed",
                    "Either seal the configuration class or ensure virtual members are intentional"));
            }

            // Check for validation attributes on required properties
            var properties = type.GetProperties();
            var hasAnyValidation = properties.Any(p => p.GetCustomAttributes()
                .Any(attr => attr.GetType().Name.Contains("Required") ||
                           attr.GetType().Name.Contains("Range") ||
                           attr.GetType().Name.Contains("StringLength")));

            if (!hasAnyValidation && properties.Length > 0)
            {
                violations.Add(CreateViolation(type,
                    $"Configuration class '{type.Name}' should have validation attributes on critical properties",
                    "Add [Required], [Range], or other validation attributes to configuration properties"));
            }
        }
    }

    private static bool IsControllerOrMinimalApi(Type type) =>
        type.Name.EndsWith("Controller") ||
        type.Name.EndsWith("Endpoint") ||
        type.GetCustomAttributes().Any(attr => 
            attr.GetType().Name.Contains("ApiController") ||
            attr.GetType().Name.Contains("Route"));

    private static bool IsConfigurationOptionsType(Type type) =>
        type.Name.EndsWith("Options") ||
        type.Name.EndsWith("Settings") ||
        type.Name.EndsWith("Config") ||
        type.Name.EndsWith("Configuration");

    private static bool IsValidOptionsType(Type type) =>
        type.IsClass &&
        !type.IsAbstract &&
        type.GetConstructors().Any(c => c.GetParameters().Length == 0) &&
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any();
}