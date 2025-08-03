using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Configuration;

/// <summary>
/// Rule to validate environment-specific configuration patterns and practices.
/// Ensures proper environment configuration management without hardcoded values.
/// </summary>
public sealed class EnvironmentConfigRule : ArchitectureRuleBase
{
    public override string RuleId => "CFG003";
    public override string Name => "Environment Configuration Rule";
    public override string Description => "Validates environment-specific configuration patterns";
    public override string Category => "Configuration";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            foreach (var type in context.Types.Where(t => !t.IsAbstract && !t.IsInterface &&
                t.Namespace?.Contains("ArchitectureTests", StringComparison.OrdinalIgnoreCase) != true))
            {
                // Check for hardcoded environment values
                ValidateHardcodedValues(type, violations);

                // Check environment-specific configuration patterns
                ValidateEnvironmentConfiguration(type, violations);

                // Check for proper environment variable usage
                ValidateEnvironmentVariableUsage(type, violations);

                // Check configuration conditional logic
                ValidateEnvironmentConditionals(type, violations);

                // Validate environment-specific settings classes
                if (IsEnvironmentConfigurationClass(type))
                {
                    ValidateEnvironmentConfigurationClass(type, violations);
                }
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsEnvironmentConfigurationClass(Type type)
    {
        return type.Name.Contains("Environment", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Config", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Settings", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Options", StringComparison.OrdinalIgnoreCase);
    }

    private void ValidateHardcodedValues(Type type, List<RuleViolation> violations)
    {
        // Check for hardcoded constants that should be configuration
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        
        foreach (var field in fields)
        {
            if (field.IsLiteral || (field.IsInitOnly && field.IsStatic))
            {
                var value = field.GetValue(null)?.ToString();
                if (IsHardcodedConfigurationValue(value))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Hardcoded configuration value found in field '{field.Name}': '{value}'. Move to configuration",
                        "Move hardcoded values to appsettings.json and environment-specific overrides"));
                }
            }
        }

        // Check for hardcoded strings in properties
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var property in properties)
        {
            if (property.CanRead && property.GetMethod != null)
            {
                // Check for properties with default values that might be environment-specific
                if (property.PropertyType == typeof(string) && 
                    IsEnvironmentSpecificProperty(property.Name))
                {
                    // Check if property has a default value
                    try
                    {
                        var instance = Activator.CreateInstance(type);
                        var value = property.GetValue(instance)?.ToString();
                        
                        if (!string.IsNullOrEmpty(value) && IsHardcodedConfigurationValue(value))
                        {
                            violations.Add(CreateViolation(
                                type,
                                $"Property '{property.Name}' has hardcoded default value that should be environment-configurable",
                                "Remove default values and use configuration binding"));
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
                }
            }
        }
    }

    private void ValidateEnvironmentConfiguration(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var method in methods)
        {
            // Check for environment detection logic
            if (method.Name.Contains("Environment", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("IsDevelopment", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("IsProduction", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("IsStaging", StringComparison.OrdinalIgnoreCase))
            {
                ValidateEnvironmentDetectionMethod(type, method, violations);
            }

            // Check for configuration methods that should be environment-aware
            if (method.Name.Contains("Configure", StringComparison.OrdinalIgnoreCase))
            {
                ValidateConfigurationMethod(type, method, violations);
            }
        }
    }

    private void ValidateEnvironmentVariableUsage(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var method in methods)
        {
            // This is a simplified check - in practice you might analyze IL or use other techniques
            // to detect Environment.GetEnvironmentVariable calls
            if (method.Name.Contains("GetEnvironmentVariable", StringComparison.OrdinalIgnoreCase) ||
                method.GetParameters().Any(p => p.Name?.Contains("environmentVariable", StringComparison.OrdinalIgnoreCase) == true))
            {
                ValidateDirectEnvironmentVariableAccess(type, method, violations);
            }
        }

        // Check for environment variable naming patterns in string constants
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        
        foreach (var field in fields)
        {
            if (field.IsLiteral && field.FieldType == typeof(string))
            {
                var value = field.GetValue(null)?.ToString();
                if (IsEnvironmentVariableName(value))
                {
                    // This might be acceptable, but check if it follows conventions
                    ValidateEnvironmentVariableName(type, field, value, violations);
                }
            }
        }
    }

    private void ValidateEnvironmentConditionals(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var method in methods)
        {
            // Check for conditional compilation or runtime environment checks
            var parameters = method.GetParameters();
            
            if (parameters.Any(p => p.ParameterType.Name.Contains("IWebHostEnvironment") ||
                                   p.ParameterType.Name.Contains("IHostEnvironment")))
            {
                // Good - using proper environment abstraction
                continue;
            }

            // Check for string-based environment checks (potential issue)
            if (method.Name.Contains("Configure", StringComparison.OrdinalIgnoreCase) &&
                parameters.Any(p => p.ParameterType == typeof(string) && 
                               (p.Name?.Contains("environment", StringComparison.OrdinalIgnoreCase) == true ||
                                p.Name?.Contains("env", StringComparison.OrdinalIgnoreCase) == true)))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Method '{method.Name}' uses string-based environment detection. Use IWebHostEnvironment instead",
                    "Replace string parameters with IWebHostEnvironment for type-safe environment detection"));
            }
        }
    }

    private void ValidateEnvironmentConfigurationClass(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var property in properties)
        {
            // Environment configuration properties should not have hardcoded defaults
            if (HasHardcodedDefault(type, property))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Environment configuration property '{property.Name}' should not have hardcoded defaults",
                    "Remove hardcoded defaults and rely on configuration binding"));
            }

            // Environment-specific properties should have appropriate validation
            if (IsEnvironmentCriticalProperty(property.Name))
            {
                var hasValidation = property.GetCustomAttributes()
                    .Any(attr => IsValidationAttribute(attr.GetType()));

                if (!hasValidation)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Environment-critical property '{property.Name}' should have validation attributes",
                        "Add [Required] and other appropriate validation attributes"));
                }
            }
        }

        // Environment configuration classes should be in appropriate namespace
        if (!type.Namespace?.Contains("Configuration", StringComparison.OrdinalIgnoreCase) == true)
        {
            violations.Add(CreateViolation(
                type,
                $"Environment configuration class '{type.Name}' should be in Configuration namespace",
                "Move environment config classes to Configuration namespace"));
        }
    }

    private void ValidateEnvironmentDetectionMethod(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        var parameters = method.GetParameters();
        
        // Environment detection should use proper abstractions
        if (!parameters.Any(p => p.ParameterType.Name.Contains("IWebHostEnvironment") ||
                                p.ParameterType.Name.Contains("IHostEnvironment")))
        {
            violations.Add(CreateViolation(
                type,
                $"Environment detection method '{method.Name}' should use IWebHostEnvironment parameter",
                "Add IWebHostEnvironment parameter for proper environment detection"));
        }

        // Return type should be boolean for Is* methods
        if (method.Name.StartsWith("Is", StringComparison.OrdinalIgnoreCase) && 
            method.ReturnType != typeof(bool))
        {
            violations.Add(CreateViolation(
                type,
                $"Environment detection method '{method.Name}' should return boolean",
                "Change return type to bool for Is* environment methods"));
        }
    }

    private void ValidateConfigurationMethod(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        var parameters = method.GetParameters();
        
        // Configuration methods should have environment parameter
        var hasEnvironmentParam = parameters.Any(p => 
            p.ParameterType.Name.Contains("IWebHostEnvironment") ||
            p.ParameterType.Name.Contains("IHostEnvironment"));

        var hasConfigurationParam = parameters.Any(p =>
            p.ParameterType.Name.Contains("IConfiguration") ||
            p.ParameterType.Name.Contains("ConfigurationManager"));

        if (hasConfigurationParam && !hasEnvironmentParam)
        {
            violations.Add(CreateViolation(
                type,
                $"Configuration method '{method.Name}' should include IWebHostEnvironment parameter for environment-specific setup",
                "Add IWebHostEnvironment parameter to enable environment-specific configuration"));
        }
    }

    private void ValidateDirectEnvironmentVariableAccess(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        // Direct Environment.GetEnvironmentVariable usage might be acceptable in some cases,
        // but should be used carefully
        violations.Add(CreateViolation(
            type,
            $"Method '{method.Name}' appears to use direct environment variable access. Consider using IConfiguration instead",
            "Use IConfiguration.GetValue<T>() instead of Environment.GetEnvironmentVariable() for testability"));
    }

    private void ValidateEnvironmentVariableName(Type type, FieldInfo field, string? value, List<RuleViolation> violations)
    {
        if (string.IsNullOrEmpty(value))
            return;

        // Environment variables should follow naming conventions
        if (!IsValidEnvironmentVariableName(value))
        {
            violations.Add(CreateViolation(
                type,
                $"Environment variable name '{value}' in field '{field.Name}' should follow UPPER_CASE naming convention",
                "Use UPPER_CASE with underscores for environment variable names"));
        }

        // Environment variables should have application prefix
        if (!HasApplicationPrefix(value))
        {
            violations.Add(CreateViolation(
                type,
                $"Environment variable '{value}' should have application-specific prefix to avoid conflicts",
                "Add application prefix like 'AXON_' to environment variable names"));
        }
    }

    private static bool IsHardcodedConfigurationValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.Contains("://") || // URLs
               value.Contains("localhost") ||
               value.Contains("127.0.0.1") ||
               value.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Database=", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("ApiKey", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
               (value.StartsWith("http://") || value.StartsWith("https://")) ||
               IsPortNumber(value) ||
               IsConnectionString(value);
    }

    private static bool IsEnvironmentSpecificProperty(string propertyName)
    {
        return propertyName.Contains("Url", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Endpoint", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Connection", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Database", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Server", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Host", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Port", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("ApiKey", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEnvironmentVariableName(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_') &&
               value.Contains('_') &&
               value.Length > 3;
    }

    private static bool IsValidEnvironmentVariableName(string value)
    {
        return value.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_') &&
               !value.StartsWith('_') &&
               !value.EndsWith('_') &&
               !value.Contains("__");
    }

    private static bool HasApplicationPrefix(string value)
    {
        return value.StartsWith("AXON_", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("APP_", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("ASPNETCORE_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEnvironmentCriticalProperty(string propertyName)
    {
        return propertyName.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("DatabaseUrl", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("ApiEndpoint", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("BaseUrl", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("RedisUrl", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("CacheUrl", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasHardcodedDefault(Type type, PropertyInfo property)
    {
        try
        {
            var instance = Activator.CreateInstance(type);
            var value = property.GetValue(instance)?.ToString();
            return !string.IsNullOrEmpty(value) && IsHardcodedConfigurationValue(value);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (TargetParameterCountException)
        {
            return false;
        }
    }

    private static bool IsValidationAttribute(Type attributeType)
    {
        return attributeType.Name.Contains("Required") ||
               attributeType.Name.Contains("Range") ||
               attributeType.Name.Contains("StringLength") ||
               attributeType.Name.Contains("Url") ||
               attributeType.Name.Contains("RegularExpression");
    }

    private static bool IsPortNumber(string value)
    {
        return int.TryParse(value, out var port) && port > 0 && port <= 65535;
    }

    private static bool IsConnectionString(string value)
    {
        return value.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Integrated Security=", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("User ID=", StringComparison.OrdinalIgnoreCase);
    }
}