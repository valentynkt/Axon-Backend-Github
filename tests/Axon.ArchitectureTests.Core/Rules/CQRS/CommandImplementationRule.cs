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

            foreach (var commandType in commandTypes)
            {
                ValidateCommandNaming(commandType, violations);
                ValidateCommandStructure(commandType, violations);
                ValidateCommandLocation(commandType, violations);
                ValidateCommandMediatRInterface(commandType, violations);
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