using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Security;

/// <summary>
/// Rule to validate input validation patterns and security practices.
/// </summary>
public sealed class InputValidationRule : ArchitectureRuleBase
{
    public override string RuleId => "SEC003";
    public override string Name => "Input Validation";
    public override string Description => "Validates that input validation is properly implemented for security";
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
                // Check DTOs and request models
                if (IsInputModel(type))
                {
                    ValidateInputModelSecurity(type, violations);
                }

                // Check API endpoints
                if (IsApiEndpoint(type))
                {
                    ValidateEndpointInputValidation(type, violations);
                }

                // Check for SQL injection vulnerabilities
                ValidateSqlInjectionPrevention(type, violations);

                // Check for XSS vulnerabilities
                ValidateXssPrevention(type, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsInputModel(Type type)
    {
        return type.Name.EndsWith("Request", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Command", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Query", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Input", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Model", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsApiEndpoint(Type type)
    {
        return type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase);
    }

    private void ValidateInputModelSecurity(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            // Check string properties for validation attributes
            if (property.PropertyType == typeof(string))
            {
                ValidateStringProperty(type, property, violations);
            }

            // Check numeric properties for range validation
            if (IsNumericType(property.PropertyType))
            {
                ValidateNumericProperty(type, property, violations);
            }

            // Check collection properties
            if (IsCollectionType(property.PropertyType))
            {
                ValidateCollectionProperty(type, property, violations);
            }

            // Check for dangerous property names
            ValidateDangerousPropertyNames(type, property, violations);
        }
    }

    private void ValidateStringProperty(Type type, PropertyInfo property, List<RuleViolation> violations)
    {
        var attributes = property.GetCustomAttributes().ToList();
        
        var hasMaxLength = attributes.Any(attr => 
            attr.GetType().Name.Contains("MaxLength") ||
            attr.GetType().Name.Contains("StringLength"));

        var hasRequired = attributes.Any(attr => attr.GetType().Name.Contains("Required"));

        // Check for potentially dangerous string properties
        if (IsDangerousStringProperty(property.Name))
        {
            if (!hasMaxLength)
            {
                violations.Add(CreateViolation(
                    type,
                    $"String property '{property.Name}' should have MaxLength validation to prevent buffer overflow attacks",
                    "Add [MaxLength] or [StringLength] attribute"));
            }

            // Check for regex or format validation on sensitive fields
            var hasFormatValidation = attributes.Any(attr => 
                attr.GetType().Name.Contains("RegularExpression") ||
                attr.GetType().Name.Contains("EmailAddress") ||
                attr.GetType().Name.Contains("Phone") ||
                attr.GetType().Name.Contains("Url"));

            if (IsEmailField(property.Name) && !hasFormatValidation)
            {
                violations.Add(CreateViolation(
                    type,
                    $"Email property '{property.Name}' should have format validation",
                    "Add [EmailAddress] or [RegularExpression] attribute"));
            }
        }

        // Check for HTML content without proper validation
        if (IsHtmlContentField(property.Name) && !HasHtmlValidation(attributes))
        {
            violations.Add(CreateViolation(
                type,
                $"HTML content property '{property.Name}' lacks XSS protection validation",
                "Add HTML sanitization validation or encoding attributes"));
        }
    }

    private void ValidateNumericProperty(Type type, PropertyInfo property, List<RuleViolation> violations)
    {
        var attributes = property.GetCustomAttributes().ToList();
        
        var hasRangeValidation = attributes.Any(attr => attr.GetType().Name.Contains("Range"));

        // Numeric properties that could be used in calculations should have range validation
        if (IsCalculationField(property.Name) && !hasRangeValidation)
        {
            violations.Add(CreateViolation(
                type,
                $"Numeric property '{property.Name}' should have Range validation to prevent overflow attacks",
                "Add [Range] attribute with appropriate min/max values"));
        }
    }

    private void ValidateCollectionProperty(Type type, PropertyInfo property, List<RuleViolation> violations)
    {
        var attributes = property.GetCustomAttributes().ToList();
        
        var hasMaxLength = attributes.Any(attr => 
            attr.GetType().Name.Contains("MaxLength") ||
            attr.GetType().Name.Contains("MaxCount"));

        if (!hasMaxLength)
        {
            violations.Add(CreateViolation(
                type,
                $"Collection property '{property.Name}' should have MaxLength validation to prevent DoS attacks",
                "Add [MaxLength] attribute to limit collection size"));
        }
    }

    private void ValidateDangerousPropertyNames(Type type, PropertyInfo property, List<RuleViolation> violations)
    {
        var dangerousPatterns = new[]
        {
            "script", "javascript", "vbscript", "onload", "onerror", "onclick",
            "sql", "query", "command", "exec", "eval"
        };

        var propertyNameLower = property.Name.ToLowerInvariant();
        var hasDangerousPattern = dangerousPatterns.Any(pattern => propertyNameLower.Contains(pattern));

        if (hasDangerousPattern)
        {
            violations.Add(CreateViolation(
                type,
                $"Property '{property.Name}' has a potentially dangerous name that could indicate security risks",
                "Review property usage and ensure proper validation and sanitization"));
        }
    }

    private void ValidateEndpointInputValidation(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            var isHttpEndpoint = method.GetCustomAttributes()
                .Any(attr => attr.GetType().Name.StartsWith("Http", StringComparison.OrdinalIgnoreCase));

            if (isHttpEndpoint)
            {
                var parameters = method.GetParameters();
                
                foreach (var parameter in parameters)
                {
                    // Check for model validation
                    if (IsInputModel(parameter.ParameterType))
                    {
                        var hasModelValidation = method.GetCustomAttributes()
                            .Any(attr => attr.GetType().Name.Contains("ValidateModel")) ||
                            parameter.GetCustomAttributes()
                            .Any(attr => attr.GetType().Name.Contains("FromBody"));

                        // This is more about ensuring ModelState.IsValid is checked in the method body
                        // but we can't easily analyze method body with reflection alone
                    }

                    // Check for dangerous parameter types
                    if (parameter.ParameterType == typeof(string) && 
                        IsDangerousStringProperty(parameter.Name ?? ""))
                    {
                        violations.Add(CreateViolation(
                            type,
                            $"Endpoint parameter '{parameter.Name}' should be validated for security",
                            "Use model binding with validation attributes instead of raw string parameters"));
                    }
                }
            }
        }
    }

    private void ValidateSqlInjectionPrevention(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            // Look for methods that might construct SQL queries
            if (method.Name.Contains("Query", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Sql", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Execute", StringComparison.OrdinalIgnoreCase))
            {
                var hasStringParameters = method.GetParameters()
                    .Any(p => p.ParameterType == typeof(string));

                if (hasStringParameters)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Method '{method.Name}' might be vulnerable to SQL injection",
                        "Use parameterized queries, ORM, or stored procedures instead of string concatenation"));
                }
            }
        }
    }

    private void ValidateXssPrevention(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties();

        foreach (var property in properties)
        {
            if (IsHtmlContentField(property.Name) && property.PropertyType == typeof(string))
            {
                var attributes = property.GetCustomAttributes().ToList();
                
                if (!HasHtmlValidation(attributes))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"HTML content property '{property.Name}' should have XSS protection",
                        "Add HTML encoding, sanitization, or validation attributes"));
                }
            }
        }
    }

    private static bool IsDangerousStringProperty(string propertyName) =>
        propertyName.Contains("Html", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Script", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Url", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase);

    private static bool IsEmailField(string propertyName) =>
        propertyName.Contains("Email", StringComparison.OrdinalIgnoreCase);

    private static bool IsHtmlContentField(string propertyName) =>
        propertyName.Contains("Html", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Content", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Description", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Body", StringComparison.OrdinalIgnoreCase);

    private static bool IsCalculationField(string propertyName) =>
        propertyName.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Price", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Count", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Quantity", StringComparison.OrdinalIgnoreCase);

    private static bool HasHtmlValidation(List<Attribute> attributes) =>
        attributes.Any(attr => 
            attr.GetType().Name.Contains("AllowHtml") ||
            attr.GetType().Name.Contains("ValidateHtml") ||
            attr.GetType().Name.Contains("HtmlEncode") ||
            attr.GetType().Name.Contains("Sanitize"));

    private static bool IsNumericType(Type type) =>
        type == typeof(int) || type == typeof(int?) ||
        type == typeof(long) || type == typeof(long?) ||
        type == typeof(decimal) || type == typeof(decimal?) ||
        type == typeof(double) || type == typeof(double?) ||
        type == typeof(float) || type == typeof(float?);

    private static bool IsCollectionType(Type type) =>
        type.IsArray ||
        (type.IsGenericType && 
         (type.GetGenericTypeDefinition() == typeof(List<>) ||
          type.GetGenericTypeDefinition() == typeof(IList<>) ||
          type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
          type.GetGenericTypeDefinition() == typeof(IEnumerable<>)));
}