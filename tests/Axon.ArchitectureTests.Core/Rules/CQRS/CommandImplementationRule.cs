using System.Reflection;
using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using MediatR;

namespace Axon.ArchitectureTests.Core.Rules.CQRS;

/// <summary>
/// Validates that Commands are properly implemented following CQRS patterns.
/// </summary>
public sealed class CommandImplementationRule : PatternComplianceRule
{
    public override string RuleId => "CQRS001";
    public override string Name => "Command Implementation Rule";
    public override string Description => "Commands must implement IRequest<T> or IRequest, be records, and follow naming conventions";
    public override string Category => "CQRS";

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var commandTypes = context.Types
                .Where(t => IsCommandType(t))
                .ToList();

            foreach var commandType in commandTypes)
            {
                ValidateCommandNaming(commandType, violations);
                ValidateCommandStructure(commandType, violations);
                ValidateCommandLocation(commandType, violations);
                ValidateCommandMediatRInterface(commandType, violations);
                ValidateCommandValidation(commandType, violations);
                ValidateCommandProperties(commandType, violations);
                ValidateCommandSideEffects(commandType, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsCommandType(Type type) =>
        type.Name.EndsWith("Command", StringComparison.OrdinalIgnoreCase) ||
        IsInCommandsNamespace(type);

    private static bool IsInCommandsNamespace(Type type) =>
        type.Namespace?.Contains(".Commands.", StringComparison.OrdinalIgnoreCase) == true ||
        type.Namespace?.EndsWith(".Commands", StringComparison.OrdinalIgnoreCase) == true;

    private void ValidateCommandNaming(Type commandType, List<RuleViolation> violations)
    {
        if (!commandType.Name.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                commandType,
                "Command types must end with 'Command' suffix",
                $"Rename '{commandType.Name}' to '{commandType.Name}Command'"));
        }
    }

    private void ValidateCommandStructure(Type commandType, List<RuleViolation> violations)
    {
        if (!commandType.IsValueType && !IsRecord(commandType))
        {
            violations.Add(CreateViolation(
                commandType,
                "Commands should be implemented as records for immutability",
                $"Convert '{commandType.Name}' to a record type"));
        }
    }

    private void ValidateCommandLocation(Type commandType, List<RuleViolation> violations)
    {
        if (!IsInCommandsNamespace(commandType))
        {
            violations.Add(CreateViolation(
                commandType,
                "Commands must be placed in a Commands namespace",
                $"Move '{commandType.Name}' to a .Commands namespace"));
        }
    }

    private void ValidateCommandMediatRInterface(Type commandType, List<RuleViolation> violations)
    {
        var implementsIRequest = ImplementsGenericInterface(commandType, typeof(IRequest<>)) ||
                                ImplementsInterface(commandType, typeof(IRequest));

        if (!implementsIRequest)
        {
            violations.Add(CreateViolation(
                commandType,
                "Commands must implement IRequest<T> or IRequest from MediatR",
                $"Make '{commandType.Name}' implement IRequest<T> or IRequest"));
        }
    }

    /// <summary>
    /// Validates that command has proper validation attributes or validation logic.
    /// </summary>
    private void ValidateCommandValidation(Type commandType, List<RuleViolation> violations)
    {
        var properties = GetPublicProperties(commandType);
        var hasValidationAttributes = properties.Any(prop => 
            prop.GetCustomAttributes().Any(attr => 
                attr.GetType().Name.Contains("Required") ||
                attr.GetType().Name.Contains("Range") ||
                attr.GetType().Name.Contains("StringLength") ||
                attr.GetType().Name.Contains("RegularExpression")));

        if (!hasValidationAttributes && properties.Any())
        {
            violations.Add(CreateViolation(
                commandType,
                "Commands with properties should have validation attributes",
                $"Add validation attributes to properties in '{commandType.Name}'"));
        }
    }

    /// <summary>
    /// Validates that command properties follow immutability patterns.
    /// </summary>
    private void ValidateCommandProperties(Type commandType, List<RuleViolation> violations)
    {
        var properties = GetPublicProperties(commandType);
        
        foreach (var property in properties)
        {
            if (property.CanWrite && property.SetMethod?.IsPublic == true)
            {
                violations.Add(CreateViolation(
                    commandType,
                    $"Command property '{property.Name}' should be immutable (init-only or get-only)",
                    $"Make property '{property.Name}' init-only or readonly"));
            }

            // Check for complex mutable types
            if (IsComplexMutableType(property.PropertyType))
            {
                violations.Add(CreateViolation(
                    commandType,
                    $"Command property '{property.Name}' uses mutable type '{property.PropertyType.Name}'",
                    $"Consider using immutable collections or readonly types for '{property.Name}'"));
            }
        }
    }

    /// <summary>
    /// Validates that commands indicate their intended side effects through return types.
    /// </summary>
    private void ValidateCommandSideEffects(Type commandType, List<RuleViolation> violations)
    {
        var requestInterface = commandType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

        if (requestInterface != null)
        {
            var returnType = requestInterface.GetGenericArguments()[0];
            
            // Commands returning complex data might be queries in disguise
            if (IsComplexDataType(returnType) && !IsResultType(returnType))
            {
                violations.Add(CreateViolation(
                    commandType,
                    $"Command returns complex data type '{returnType.Name}'. Consider if this should be a Query instead",
                    $"Either wrap return type in Result<T> or consider converting '{commandType.Name}' to a Query"));
            }
        }
    }

    /// <summary>
    /// Checks if a type is a complex mutable type that should be avoided in commands.
    /// </summary>
    private static bool IsComplexMutableType(Type type)
    {
        return type.IsClass && 
               type != typeof(string) && 
               !type.IsValueType &&
               !IsImmutableCollectionType(type) &&
               type.GetProperties().Any(p => p.CanWrite && p.SetMethod?.IsPublic == true);
    }

    /// <summary>
    /// Checks if a type is an immutable collection type.
    /// </summary>
    private static bool IsImmutableCollectionType(Type type)
    {
        return type.Namespace?.StartsWith("System.Collections.Immutable") == true ||
               type.Name.Contains("ReadOnly", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a type represents complex data that might indicate a query operation.
    /// </summary>
    private static bool IsComplexDataType(Type type)
    {
        return type.IsClass && 
               type != typeof(string) && 
               !type.IsValueType &&
               type.Name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Response", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Model", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a type is a Result type wrapper.
    /// </summary>
    private static bool IsResultType(Type type)
    {
        return type.Name.StartsWith("Result", StringComparison.OrdinalIgnoreCase) ||
               type.Namespace?.Contains("Result") == true;
    }

    private static bool IsRecord(Type type)
    {
        // Check if type has the compiler-generated methods that records have
        var equalsMethod = type.GetMethod("Equals", BindingFlags.Public | BindingFlags.Instance, new[] { type });
        var getHashCodeMethod = type.GetMethod("GetHashCode", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        var toStringMethod = type.GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);

        return equalsMethod != null && getHashCodeMethod != null && toStringMethod != null &&
               type.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() == null &&
               type.BaseType == typeof(object);
    }
}