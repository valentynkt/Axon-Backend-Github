using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.Configuration;

/// <summary>
/// Rule to validate logging configuration patterns and best practices.
/// Ensures proper logging configuration, structured logging, and performance considerations.
/// </summary>
public sealed class LoggingConfigRule : ArchitectureRuleBase
{
    public override string RuleId => "CFG005";
    public override string Name => "Logging Configuration Rule";
    public override string Description => "Validates logging configuration patterns and structured logging";
    public override string Category => "Configuration";
    public override RuleSeverity Severity => RuleSeverity.Warning;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            foreach (var type in context.Types.Where(ShouldValidateType))
            {
                // Check logging configuration classes
                if (IsLoggingConfigurationClass(type))
                {
                    ValidateLoggingConfiguration(type, violations);
                }

                // Check services that use logging (excluding test framework code)
                if (UsesLogging(type) && !IsTestFrameworkType(type))
                {
                    ValidateLoggingUsage(type, violations);
                }

                // Check startup/program logging setup
                if (IsStartupOrProgramClass(type))
                {
                    ValidateLoggingSetup(type, violations);
                }

                // Check for logging performance issues (but not in test framework)
                if (!IsTestFrameworkType(type))
                {
                    ValidateLoggingPerformance(type, violations);
                    ValidateLogSensitivity(type, violations);
                }
            }
        }, cancellationToken);

        return violations;
    }

    private static bool ShouldValidateType(Type type)
    {
        // Exclude test assemblies and system types from validation
        if (type.IsAbstract || type.IsInterface)
            return false;

        // Exclude test assemblies completely 
        if (IsTestAssembly(type.Assembly))
            return false;

        // Exclude system and framework types
        if (IsSystemOrFrameworkType(type))
            return false;

        return true;
    }

    private static bool IsTestAssembly(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name ?? "";
        return assemblyName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Contains("Tests", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Contains("ArchitectureTests", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestFrameworkType(Type type)
    {
        var typeName = type.FullName ?? "";
        var assemblyName = type.Assembly.GetName().Name ?? "";

        return assemblyName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("Mock", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("Fake", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("ArchitectureTest", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSystemOrFrameworkType(Type type)
    {
        var typeNamespace = type.Namespace ?? "";
        return typeNamespace.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
               typeNamespace.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
               typeNamespace.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase) ||
               typeNamespace.StartsWith("Shouldly", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoggingConfigurationClass(Type type)
    {
        return type.Name.Contains("Logging", StringComparison.OrdinalIgnoreCase) &&
               (type.Name.Contains("Options", StringComparison.OrdinalIgnoreCase) ||
                type.Name.Contains("Settings", StringComparison.OrdinalIgnoreCase) ||
                type.Name.Contains("Config", StringComparison.OrdinalIgnoreCase) ||
                type.Name.Contains("Configuration", StringComparison.OrdinalIgnoreCase));
    }

    private static bool UsesLogging(Type type)
    {
        var constructors = type.GetConstructors();
        return constructors.Any(c => c.GetParameters().Any(p => 
            p.ParameterType.Name.Contains("ILogger"))) ||
               type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                   .Any(f => f.FieldType.Name.Contains("ILogger"));
    }

    private static bool IsStartupOrProgramClass(Type type)
    {
        return type.Name.Equals("Startup", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Equals("Program", StringComparison.OrdinalIgnoreCase) ||
               type.GetMethods().Any(m => m.Name.Contains("ConfigureServices", StringComparison.OrdinalIgnoreCase) ||
                                         m.Name.Contains("ConfigureLogging", StringComparison.OrdinalIgnoreCase));
    }

    private void ValidateLoggingConfiguration(Type type, List<RuleViolation> violations)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        // Check for required logging configuration properties
        ValidateRequiredLoggingProperties(type, properties, violations);
        
        // Check for log level configuration
        ValidateLogLevelConfiguration(type, properties, violations);
        
        // Check for provider configuration
        ValidateProviderConfiguration(type, properties, violations);
        
        // Check for structured logging configuration
        ValidateStructuredLoggingConfiguration(type, properties, violations);
        
        // Check for performance configuration
        ValidatePerformanceConfiguration(type, properties, violations);
    }

    private void ValidateRequiredLoggingProperties(Type type, PropertyInfo[] properties, List<RuleViolation> violations)
    {
        var hasLogLevel = properties.Any(p => 
            p.Name.Contains("LogLevel", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Level", StringComparison.OrdinalIgnoreCase));

        if (!hasLogLevel)
        {
            violations.Add(CreateViolation(
                type,
                $"Logging configuration '{type.Name}' should have LogLevel configuration",
                "Add LogLevel property to control logging verbosity"));
        }

        var hasProviders = properties.Any(p => 
            p.Name.Contains("Providers", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Console", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("File", StringComparison.OrdinalIgnoreCase));

        if (!hasProviders)
        {
            violations.Add(CreateViolation(
                type,
                $"Logging configuration '{type.Name}' should specify logging providers",
                "Add properties for logging providers (Console, File, etc.)"));
        }
    }

    private void ValidateLogLevelConfiguration(Type type, PropertyInfo[] properties, List<RuleViolation> violations)
    {
        foreach (var property in properties)
        {
            if (property.Name.Contains("LogLevel", StringComparison.OrdinalIgnoreCase))
            {
                // Log level properties should have validation
                var hasValidation = property.GetCustomAttributes()
                    .Any(attr => IsValidationAttribute(attr.GetType()));

                if (!hasValidation)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"LogLevel property '{property.Name}' should have validation attributes",
                        "Add [Required] or [Range] attributes to validate log levels"));
                }

                // Check for appropriate default values
                ValidateLogLevelDefaults(type, property, violations);
            }
        }
    }

    private void ValidateProviderConfiguration(Type type, PropertyInfo[] properties, List<RuleViolation> violations)
    {
        var providerProperties = properties.Where(p => 
            p.Name.Contains("Console", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("File", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("EventLog", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Database", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("ApplicationInsights", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Serilog", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var property in providerProperties)
        {
            // Provider configurations should be properly structured
            if (property.PropertyType == typeof(bool))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Provider property '{property.Name}' should use configuration object instead of boolean",
                    "Use structured configuration objects for logging providers"));
            }

            // File logging should have path configuration
            if (property.Name.Contains("File", StringComparison.OrdinalIgnoreCase))
            {
                ValidateFileLoggingConfiguration(type, property, violations);
            }
        }
    }

    private void ValidateStructuredLoggingConfiguration(Type type, PropertyInfo[] properties, List<RuleViolation> violations)
    {
        var hasStructuredLogging = properties.Any(p => 
            p.Name.Contains("Structured", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Json", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Serilog", StringComparison.OrdinalIgnoreCase));

        if (!hasStructuredLogging)
        {
            violations.Add(CreateViolation(
                type,
                $"Logging configuration '{type.Name}' should support structured logging",
                "Add structured logging configuration for better log analysis and monitoring"));
        }

        // Check for correlation ID configuration
        var hasCorrelationId = properties.Any(p => 
            p.Name.Contains("CorrelationId", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("TraceId", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("RequestId", StringComparison.OrdinalIgnoreCase));

        if (!hasCorrelationId)
        {
            violations.Add(CreateViolation(
                type,
                $"Logging configuration '{type.Name}' should include correlation ID support",
                "Add correlation ID configuration for request tracking"));
        }
    }

    private void ValidatePerformanceConfiguration(Type type, PropertyInfo[] properties, List<RuleViolation> violations)
    {
        // Check for async logging configuration
        var hasAsyncLogging = properties.Any(p => 
            p.Name.Contains("Async", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Background", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Queue", StringComparison.OrdinalIgnoreCase));

        if (!hasAsyncLogging)
        {
            violations.Add(CreateViolation(
                type,
                $"Logging configuration '{type.Name}' should consider async logging for performance",
                "Add async logging configuration to prevent blocking application threads"));
        }

        // Check for buffer size configuration
        var hasBufferConfig = properties.Any(p => 
            p.Name.Contains("Buffer", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Batch", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Flush", StringComparison.OrdinalIgnoreCase));

        if (!hasBufferConfig && hasAsyncLogging)
        {
            violations.Add(CreateViolation(
                type,
                $"Async logging configuration should include buffer settings",
                "Add buffer size and flush interval configuration for optimal performance"));
        }
    }

    private void ValidateLoggingUsage(Type type, List<RuleViolation> violations)
    {
        // Only validate production code, not test code
        if (IsTestFrameworkType(type))
            return;

        // Check constructor injection of ILogger
        ValidateLoggerInjection(type, violations);
        
        // Check logging method usage
        ValidateLoggingMethodUsage(type, violations);
        
        // Check for structured logging usage
        ValidateStructuredLoggingUsage(type, violations);
        
        // Check for log scopes
        ValidateLogScopesUsage(type, violations);
    }

    private void ValidateLoggerInjection(Type type, List<RuleViolation> violations)
    {
        var constructors = type.GetConstructors();
        var loggerParameters = new List<ParameterInfo>();

        foreach (var constructor in constructors)
        {
            var loggerParams = constructor.GetParameters()
                .Where(p => p.ParameterType.Name.Contains("ILogger"))
                .ToList();
            
            loggerParameters.AddRange(loggerParams);
        }

        if (loggerParameters.Any())
        {
            // Check for generic vs non-generic logger
            foreach (var param in loggerParameters)
            {
                if (param.ParameterType.IsGenericType)
                {
                    var genericArg = param.ParameterType.GetGenericArguments().FirstOrDefault();
                    if (genericArg != type)
                    {
                        violations.Add(CreateViolation(
                            type,
                            $"ILogger<T> generic argument should match the containing type. Expected ILogger<{type.Name}>",
                            $"Change to ILogger<{type.Name}> for proper logger categorization"));
                    }
                }
                else
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Use ILogger<{type.Name}> instead of non-generic ILogger for better categorization",
                        "Use generic ILogger<T> for automatic category assignment"));
                }

                // Check parameter naming
                if (!param.Name?.EndsWith("logger", StringComparison.OrdinalIgnoreCase) == true)
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Logger parameter should be named 'logger', not '{param.Name}'",
                        "Use consistent 'logger' naming for logger parameters"));
                }
            }
        }
    }

    private void ValidateLoggingMethodUsage(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        foreach (var method in methods)
        {
            // Skip if this is test framework code
            if (IsTestFrameworkType(type))
                continue;

            // Check for direct string concatenation in logging
            if (HasStringConcatenationInLogging(method))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Method '{method.Name}' appears to use string concatenation in logging. Use structured logging instead",
                    "Use structured logging with message templates: logger.LogInformation(\"User {UserId} logged in\", userId)"));
            }

            // Check for expensive operations in log messages
            if (HasExpensiveOperationsInLogging(method))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Method '{method.Name}' may perform expensive operations in log messages",
                    "Use log level checks or LoggerMessage source generators for performance-critical logging"));
            }

            // Check for proper exception logging
            ValidateExceptionLogging(type, method, violations);
        }
    }

    private void ValidateStructuredLoggingUsage(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        foreach (var method in methods)
        {
            if (HasLoggingCalls(method) && !IsTestFrameworkType(type))
            {
                // Check for structured logging patterns
                if (!UsesStructuredLogging(method))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Method '{method.Name}' should use structured logging with message templates",
                        "Use message templates like: logger.LogInformation(\"Processing {ItemCount} items\", count)"));
                }

                // Check for semantic logging
                if (!UsesSemanticLogging(method))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Method '{method.Name}' should use semantic logging with meaningful context",
                        "Include relevant context properties in log messages for better observability"));
                }
            }
        }
    }

    private void ValidateLogScopesUsage(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        foreach (var method in methods)
        {
            // Skip test framework types
            if (IsTestFrameworkType(type))
                continue;

            // Check for operations that should use log scopes
            if (ShouldUseLogScope(method) && !UsesLogScope(method))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Method '{method.Name}' should use log scopes for better context tracking",
                    "Use using(logger.BeginScope(new { OperationId = operationId })) for operation boundaries"));
            }
        }
    }

    private void ValidateLoggingSetup(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        
        foreach (var method in methods)
        {
            if (method.Name.Contains("ConfigureLogging", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("AddLogging", StringComparison.OrdinalIgnoreCase))
            {
                ValidateLoggingConfigurationMethod(type, method, violations);
            }
        }
    }

    private void ValidateLoggingPerformance(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        foreach (var method in methods)
        {
            // Check for performance anti-patterns
            if (HasLoggingPerformanceIssues(method))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Method '{method.Name}' has potential logging performance issues",
                    "Use LoggerMessage source generators or check log levels before expensive operations"));
            }

            // Check for excessive logging
            if (HasExcessiveLogging(method))
            {
                violations.Add(CreateViolation(
                    type,
                    $"Method '{method.Name}' may have excessive logging that could impact performance",
                    "Review logging frequency and consider using Trace level for detailed diagnostics"));
            }
        }
    }

    private void ValidateLogSensitivity(Type type, List<RuleViolation> violations)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        foreach (var method in methods)
        {
            if (HasLoggingCalls(method))
            {
                // Check for potential sensitive data in logs
                if (MayLogSensitiveData(method))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Method '{method.Name}' may log sensitive data. Implement log sanitization",
                        "Sanitize sensitive data or use structured logging to exclude sensitive properties"));
                }

                // Check for PII in logs
                if (MayLogPersonallyIdentifiableInformation(method))
                {
                    violations.Add(CreateViolation(
                        type,
                        $"Method '{method.Name}' may log personally identifiable information (PII)",
                        "Ensure PII is not logged or implement proper data anonymization"));
                }
            }
        }
    }

    // Helper methods for validation logic
    private void ValidateLogLevelDefaults(Type type, PropertyInfo property, List<RuleViolation> violations)
    {
        try
        {
            var instance = Activator.CreateInstance(type);
            var value = property.GetValue(instance)?.ToString();
            
            if (!string.IsNullOrEmpty(value) && value.Equals("Debug", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(CreateViolation(
                    type,
                    $"LogLevel property '{property.Name}' should not default to Debug in production configuration",
                    "Use Information or Warning as default log level for production"));
            }
        }
        catch (Exception ex) when (ex is ArgumentException or TargetParameterCountException or InvalidOperationException)
        {
            // Ignore reflection errors
        }
    }

    private void ValidateFileLoggingConfiguration(Type type, PropertyInfo property, List<RuleViolation> violations)
    {
        // File logging should have path and rotation configuration
        var parentType = property.DeclaringType;
        var properties = parentType?.GetProperties() ?? type.GetProperties();
        
        var hasPathConfig = properties.Any(p => 
            p.Name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("File", StringComparison.OrdinalIgnoreCase));

        if (!hasPathConfig)
        {
            violations.Add(CreateViolation(
                type,
                $"File logging configuration should include file path settings",
                "Add file path configuration for file logging providers"));
        }

        var hasRotationConfig = properties.Any(p => 
            p.Name.Contains("Rotation", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("RollingFile", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("MaxFileSize", StringComparison.OrdinalIgnoreCase));

        if (!hasRotationConfig)
        {
            violations.Add(CreateViolation(
                type,
                $"File logging configuration should include log rotation settings",
                "Add log rotation configuration to prevent disk space issues"));
        }
    }

    // Detection helper methods
    private static bool IsValidationAttribute(Type attributeType)
    {
        return attributeType.Name.Contains("Required") ||
               attributeType.Name.Contains("Range") ||
               attributeType.Name.Contains("StringLength");
    }

    private static bool HasStringConcatenationInLogging(MethodInfo method)
    {
        // Simplified check - in practice you would analyze method body
        return method.GetParameters().Any(p => p.ParameterType == typeof(string) && 
                                               p.Name?.Contains("message", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool HasExpensiveOperationsInLogging(MethodInfo method)
    {
        // Simplified check for method calls that might be expensive in logging
        return method.Name.Contains("ToString", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Serialize", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasLoggingCalls(MethodInfo method)
    {
        return method.Name.Contains("Log", StringComparison.OrdinalIgnoreCase) ||
               method.GetParameters().Any(p => p.ParameterType.Name.Contains("ILogger"));
    }

    private static bool UsesStructuredLogging(MethodInfo method)
    {
        // Simplified check - would need method body analysis
        return method.GetParameters().Any(p => p.Name?.Contains("template", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool UsesSemanticLogging(MethodInfo method)
    {
        // Simplified check for semantic logging patterns
        return method.GetParameters().Length > 2; // Message template + parameters
    }

    private static bool ShouldUseLogScope(MethodInfo method)
    {
        return method.Name.Contains("Process", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Execute", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Handle", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Operation", StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsesLogScope(MethodInfo method)
    {
        // Simplified check - would need method body analysis
        return method.GetParameters().Any(p => p.Name?.Contains("scope", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool HasLoggingPerformanceIssues(MethodInfo method)
    {
        // Check for potential performance issues in logging
        return method.GetParameters().Any(p => p.ParameterType == typeof(object[])) ||
               method.Name.Contains("ToJson", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasExcessiveLogging(MethodInfo method)
    {
        // Simplified check for methods that might log too frequently
        return method.Name.Contains("Loop", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Iterate", StringComparison.OrdinalIgnoreCase) ||
               method.Name.Contains("Each", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MayLogSensitiveData(MethodInfo method)
    {
        return method.GetParameters().Any(p => 
            p.Name?.Contains("password", StringComparison.OrdinalIgnoreCase) == true ||
            p.Name?.Contains("secret", StringComparison.OrdinalIgnoreCase) == true ||
            p.Name?.Contains("token", StringComparison.OrdinalIgnoreCase) == true ||
            p.Name?.Contains("key", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool MayLogPersonallyIdentifiableInformation(MethodInfo method)
    {
        return method.GetParameters().Any(p => 
            p.Name?.Contains("email", StringComparison.OrdinalIgnoreCase) == true ||
            p.Name?.Contains("phone", StringComparison.OrdinalIgnoreCase) == true ||
            p.Name?.Contains("address", StringComparison.OrdinalIgnoreCase) == true ||
            p.Name?.Contains("ssn", StringComparison.OrdinalIgnoreCase) == true ||
            p.Name?.Contains("creditcard", StringComparison.OrdinalIgnoreCase) == true);
    }

    private void ValidateExceptionLogging(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        var parameters = method.GetParameters();
        var hasExceptionParam = parameters.Any(p => p.ParameterType.IsSubclassOf(typeof(Exception)));

        if (hasExceptionParam)
        {
            // Exception logging should follow proper patterns
            violations.Add(CreateViolation(
                type,
                $"Method '{method.Name}' logs exceptions. Ensure proper exception logging patterns are used",
                "Use logger.LogError(exception, message) pattern for proper exception logging"));
        }
    }

    private void ValidateLoggingConfigurationMethod(Type type, MethodInfo method, List<RuleViolation> violations)
    {
        var parameters = method.GetParameters();
        
        // Logging configuration should include builder or services parameter
        var hasBuilderParam = parameters.Any(p => 
            p.ParameterType.Name.Contains("Builder") ||
            p.ParameterType.Name.Contains("ServiceCollection"));

        if (!hasBuilderParam)
        {
            violations.Add(CreateViolation(
                type,
                $"Logging configuration method '{method.Name}' should accept builder or services parameter",
                "Add IServiceCollection or ILoggingBuilder parameter to configuration methods"));
        }

        // Should configure multiple aspects of logging
        if (method.Name.Contains("Configure", StringComparison.OrdinalIgnoreCase) && 
            !method.Name.Contains("Logging", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                type,
                $"Configuration method '{method.Name}' should include logging setup",
                "Add logging configuration to service configuration methods"));
        }
    }
}