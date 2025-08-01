using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Configuration;

/// <summary>
/// Rule to validate configuration validation patterns and data annotation compliance.
/// Ensures proper validation is implemented for all configuration classes.
/// </summary>
public sealed class ConfigurationValidationRule : ArchitectureRuleBase
{
    public override string RuleId => "CFG002";
    public override string Name => "Configuration Validation Rule";
    public override string Description => "Validates configuration validation patterns and data annotations";
    public override string Category => "Configuration";
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
                // Check configuration classes for validation
                if (IsConfigurationClass(type))
                {
                    ValidateConfigurationValidation(type, violations);
                    ValidateDataAnnotations(type, violations);
                    ValidateCustomValidation(type, violations);
                }

                // Check services that validate configuration
                if (IsConfigurationValidator(type))
                {
                    ValidateValidatorImplementation(type, violations);
                }

                // Check startup/program configuration
                if (IsStartupOrProgramClass(type))
                {
                    ValidateConfigurationSetup(type, violations);
                }
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsConfigurationClass(Type type)
    {
        return type.Name.EndsWith("Options", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Settings", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Config", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Configuration", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfigurationValidator(Type type)
    {
        return type.Name.Contains("Validator", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("IValidator")) ||
               type.GetMethods().Any(m => m.Name.Contains("Validate", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStartupOrProgramClass(Type type)
    {
        return type.Name.Equals("Startup", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Equals("Program", StringComparison.OrdinalIgnoreCase) ||
               type.GetMethods().Any(m => m.Name.Contains("ConfigureServices", StringComparison.OrdinalIgnoreCase));
    }

    private void ValidateConfigurationValidation(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var hasAnyValidation = false;

        foreach (var property in properties)
        {
            var validationAttributes = property.GetCustomAttributes()
                .Where(attr => IsValidationAttribute(attr.GetType()))
                .ToList();

            if (validationAttributes.Any())
            {
                hasAnyValidation = true;
                
                // Validate validation attribute combinations
                ValidateAttributeCombinations(type, property, validationAttributes, violations);
            }

            // Check for properties that should have validation
            if (ShouldHaveValidation(property))
            {
                if (!validationAttributes.Any())
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Property '{property.Name}' should have validation attributes based on its type and naming",
                        GetValidationSuggestion(property)));
                }
            }

            // Check for validation on sensitive properties
            if (IsSensitiveProperty(property.Name))
            {
                if (!validationAttributes.Any())
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Sensitive property '{property.Name}' must have validation attributes",
                        "Add [Required] and appropriate length/format validation attributes"));
                }
                
                // Sensitive properties should have additional validation
                var hasStrongValidation = validationAttributes.Any(attr => 
                    IsStrongValidationAttribute(attr.GetType()));
                
                if (!hasStrongValidation)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Sensitive property '{property.Name}' should have strong validation (RegularExpression, custom validators)",
                        "Add [RegularExpression] or custom validation attributes for sensitive data"));
                }
            }
        }

        // Configuration classes with validation should implement IValidatableObject
        if (hasAnyValidation && !ImplementsIValidatableObject(type))
        {
            violations.Add(CreateViolation(
                type,
                $"Configuration class '{type.Name}' with validation attributes should implement IValidatableObject for cross-property validation",
                "Implement IValidatableObject interface and provide Validate method"));
        }
    }

    private void ValidateDataAnnotations(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var validationAttributes = property.GetCustomAttributes()
                .Where(attr => IsValidationAttribute(attr.GetType()))
                .ToList();

            foreach (var attribute in validationAttributes)
            {
                // Validate specific attribute usage
                ValidateSpecificAttribute(type, property, attribute, violations);
            }

            // Check for conflicting validation attributes
            ValidateAttributeConflicts(type, property, validationAttributes, violations);

            // Check for missing error messages
            ValidateErrorMessages(type, property, validationAttributes, violations);
        }
    }

    private void ValidateCustomValidation(Type type, List<RuleViolation> violations)
    {
        if (!ImplementsIValidatableObject(type))
            return;

        var validateMethod = type.GetMethod("Validate", BindingFlags.Public | BindingFlags.Instance);
        if (validateMethod == null)
        {
            violations.Add(CreateViolation(
                type,
                $"Class '{type.Name}' implements IValidatableObject but doesn't have a Validate method",
                "Implement the Validate method from IValidatableObject interface"));
            return;
        }

        // Check that Validate method returns IEnumerable<ValidationResult>
        var returnType = validateMethod.ReturnType;
        if (!returnType.Name.Contains("IEnumerable") || 
            !returnType.GetGenericArguments().Any(t => t.Name.Contains("ValidationResult")))
        {
            violations.Add(CreateViolation(
                type,
                $"Validate method in '{type.Name}' should return IEnumerable<ValidationResult>",
                "Change return type to IEnumerable<ValidationResult>"));
        }

        // Check method parameters
        var parameters = validateMethod.GetParameters();
        if (parameters.Length != 1 || !parameters[0].ParameterType.Name.Contains("ValidationContext"))
        {
            violations.Add(CreateViolation(
                type,
                $"Validate method in '{type.Name}' should take ValidationContext as parameter",
                "Change method signature to Validate(ValidationContext validationContext)"));
        }
    }

    private void ValidateValidatorImplementation(Type type, List<RuleViolation> violations)
    {
        // Check for proper validator patterns
        if (type.Name.Contains("Validator", StringComparison.OrdinalIgnoreCase))
        {
            var validateMethods = type.GetMethods()
                .Where(m => m.Name.Contains("Validate", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!validateMethods.Any())
            {
                violations.Add(CreateViolation(
                    type,
                    $"Validator class '{type.Name}' should have Validate methods",
                    "Add validation methods to validator classes"));
            }

            foreach (var method in validateMethods)
            {
                // Validate method signatures
                if (method.ReturnType == typeof(void))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Validation method '{method.Name}' should return validation results, not void",
                        "Change return type to bool, ValidationResult, or IEnumerable<ValidationResult>"));
                }
            }
        }
    }

    private void ValidateConfigurationSetup(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        
        foreach (var method in methods)
        {
            if (method.Name.Contains("ConfigureServices", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("AddOptions", StringComparison.OrdinalIgnoreCase))
            {
                // Check for options validation setup
                // This is a simplified check - in practice you might analyze method body
                if (!HasOptionsValidationSetup(method))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Configuration setup method '{method.Name}' should include options validation",
                        "Add .ValidateDataAnnotations() and .ValidateOnStart() calls to options configuration"));
                }
            }
        }
    }

    private void ValidateAttributeCombinations(Type type, PropertyInfo property, 
        List<Attribute> validationAttributes, List<RuleViolation> violations)
    {
        var attributeTypes = validationAttributes.Select(a => a.GetType().Name).ToList();

        // Check for conflicting length attributes
        if (attributeTypes.Contains("MinLengthAttribute") && 
            attributeTypes.Contains("StringLengthAttribute"))
        {
            violations.Add(CreateViolation(
                type,
                $"Property '{property.Name}' has both MinLength and StringLength attributes. Use StringLength with MinimumLength instead",
                "Replace MinLength + StringLength with single StringLength attribute"));
        }

        // Check for redundant required attributes
        var requiredCount = attributeTypes.Count(t => t.Contains("Required"));
        if (requiredCount > 1)
        {
            violations.Add(CreateViolation(
                type,
                $"Property '{property.Name}' has multiple Required-type attributes",
                "Use only one Required attribute per property"));
        }
    }

    private void ValidateAttributeConflicts(Type type, PropertyInfo property, 
        List<Attribute> validationAttributes, List<RuleViolation> violations)
    {
        // Check for logical conflicts in validation rules
        foreach (var attribute in validationAttributes)
        {
            if (attribute.GetType().Name == "RangeAttribute")
            {
                // Range validation on string properties might be incorrect
                if (property.PropertyType == typeof(string))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Property '{property.Name}' has Range attribute on string type. Consider StringLength instead",
                        "Use StringLength attribute for string length validation"));
                }
            }

            if (attribute.GetType().Name == "StringLengthAttribute")
            {
                // StringLength on non-string properties
                if (property.PropertyType != typeof(string))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Property '{property.Name}' has StringLength attribute on non-string type",
                        "Use Range attribute for numeric validation or remove StringLength"));
                }
            }
        }
    }

    private void ValidateErrorMessages(Type type, PropertyInfo property, 
        List<Attribute> validationAttributes, List<RuleViolation> violations)
    {
        foreach (var attribute in validationAttributes)
        {
            // Check if validation attributes have custom error messages
            var errorMessageProperty = attribute.GetType().GetProperty("ErrorMessage");
            if (errorMessageProperty != null)
            {
                var errorMessage = errorMessageProperty.GetValue(attribute) as string;
                if (string.IsNullOrEmpty(errorMessage))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Validation attribute on property '{property.Name}' should have custom error message",
                        "Add ErrorMessage property to validation attributes for better user experience"));
                }
            }
        }
    }

    private void ValidateSpecificAttribute(Type type, PropertyInfo property, 
        Attribute attribute, List<RuleViolation> violations)
    {
        var attributeType = attribute.GetType();

        // Validate RegularExpression attributes
        if (attributeType.Name == "RegularExpressionAttribute")
        {
            var patternProperty = attributeType.GetProperty("Pattern");
            if (patternProperty != null)
            {
                var pattern = patternProperty.GetValue(attribute) as string;
                if (string.IsNullOrEmpty(pattern))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"RegularExpression attribute on property '{property.Name}' has empty pattern",
                        "Provide a valid regular expression pattern"));
                }
                else if (IsWeakRegexPattern(pattern))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"RegularExpression pattern on property '{property.Name}' appears to be too permissive",
                        "Use more specific regular expression patterns for better validation"));
                }
            }
        }

        // Validate Range attributes
        if (attributeType.Name == "RangeAttribute")
        {
            var minimumProperty = attributeType.GetProperty("Minimum");
            var maximumProperty = attributeType.GetProperty("Maximum");
            
            if (minimumProperty != null && maximumProperty != null)
            {
                _ = minimumProperty.GetValue(attribute);
                _ = maximumProperty.GetValue(attribute);
                
                // Basic range validation logic would go here
                // This is simplified for demonstration
            }
        }
    }

    private static bool ShouldHaveValidation(PropertyInfo property)
    {
        // Properties that typically should have validation
        return property.Name.Contains("Url", StringComparison.OrdinalIgnoreCase) ||
               property.Name.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
               property.Name.Contains("Phone", StringComparison.OrdinalIgnoreCase) ||
               property.Name.Contains("Port", StringComparison.OrdinalIgnoreCase) ||
               property.Name.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ||
               property.Name.Contains("MaxRetries", StringComparison.OrdinalIgnoreCase) ||
               property.Name.Contains("MinRetries", StringComparison.OrdinalIgnoreCase) ||
               (property.PropertyType == typeof(int) && property.Name.Contains("Count", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSensitiveProperty(string propertyName)
    {
        return propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("ApiKey", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidationAttribute(Type attributeType)
    {
        return attributeType.Name.Contains("Required") ||
               attributeType.Name.Contains("Range") ||
               attributeType.Name.Contains("StringLength") ||
               attributeType.Name.Contains("MinLength") ||
               attributeType.Name.Contains("MaxLength") ||
               attributeType.Name.Contains("RegularExpression") ||
               attributeType.Name.Contains("Email") ||
               attributeType.Name.Contains("Url") ||
               attributeType.Name.Contains("Phone") ||
               attributeType.Name.Contains("CreditCard") ||
               attributeType.Name.Contains("Compare");
    }

    private static bool IsStrongValidationAttribute(Type attributeType)
    {
        return attributeType.Name.Contains("RegularExpression") ||
               attributeType.Name.Contains("Custom") ||
               attributeType.Name.Contains("Remote");
    }

    private static bool ImplementsIValidatableObject(Type type)
    {
        return type.GetInterfaces().Any(i => i.Name.Contains("IValidatableObject"));
    }

    private static string GetValidationSuggestion(PropertyInfo property)
    {
        if (property.Name.Contains("Email", StringComparison.OrdinalIgnoreCase))
            return "Add [Required] and [EmailAddress] attributes";
        
        if (property.Name.Contains("Url", StringComparison.OrdinalIgnoreCase))
            return "Add [Required] and [Url] attributes";
        
        if (property.Name.Contains("Phone", StringComparison.OrdinalIgnoreCase))
            return "Add [Required] and [Phone] attributes";
        
        if (property.PropertyType == typeof(string))
            return "Add [Required] and [StringLength] attributes";
        
        if (property.PropertyType == typeof(int))
            return "Add [Range] attribute with appropriate min/max values";
        
        return "Add appropriate validation attributes";
    }

    private static bool IsWeakRegexPattern(string pattern)
    {
        // Basic check for overly permissive regex patterns
        return pattern == ".*" || 
               pattern == ".+" || 
               pattern.Length < 3 ||
               !pattern.Contains('[') && !pattern.Contains("\\d") && !pattern.Contains("\\w");
    }

    private static bool HasOptionsValidationSetup(MethodInfo method)
    {
        // This is a simplified check - in practice you would analyze the method body
        // to look for calls to ValidateDataAnnotations() or ValidateOnStart()
        return method.GetParameters().Any(p => p.ParameterType.Name.Contains("OptionsBuilder"));
    }
}