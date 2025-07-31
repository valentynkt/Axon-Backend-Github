using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using MediatR;

namespace Axon.ArchitectureTests.Core.Rules.CQRS;

/// <summary>
/// Validates that Handlers are properly implemented following CQRS patterns.
/// </summary>
public sealed class HandlerImplementationRule : PatternComplianceRule
{
    public override string RuleId => "CQRS003";
    public override string Name => "Handler Implementation Rule";
    public override string Description => "Handlers must implement IRequestHandler<T,R>, be sealed, follow naming conventions, and have proper async signatures";
    public override string Category => "CQRS";

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var handlerTypes = context.Types
                .Where(t => IsHandlerType(t))
                .ToList();

            foreach (var handlerType in handlerTypes)
            {
                ValidateHandlerNaming(handlerType, violations);
                ValidateHandlerStructure(handlerType, violations);
                ValidateHandlerLocation(handlerType, violations);
                ValidateHandlerMediatRInterface(handlerType, violations);
                ValidateHandlerMethods(handlerType, violations);
            }
        }, cancellationToken);

        return violations;
    }

    private static bool IsHandlerType(Type type) =>
        type.Name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) ||
        ImplementsAnyHandlerInterface(type);

    private static bool ImplementsAnyHandlerInterface(Type type) =>
        type.GetInterfaces().Any(i => 
            i.IsGenericType && 
            (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) || 
             i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)));

    private void ValidateHandlerNaming(Type handlerType, List<RuleViolation> violations)
    {
        if (!handlerType.Name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                handlerType,
                "Handler types must end with 'Handler' suffix",
                $"Rename '{handlerType.Name}' to '{handlerType.Name}Handler'"));
        }
    }

    private void ValidateHandlerStructure(Type handlerType, List<RuleViolation> violations)
    {
        if (!IsSealed(handlerType))
        {
            violations.Add(CreateViolation(
                handlerType,
                "Handlers should be sealed to prevent inheritance",
                $"Make '{handlerType.Name}' sealed"));
        }
    }

    private void ValidateHandlerLocation(Type handlerType, List<RuleViolation> violations)
    {
        var isInHandlersNamespace = handlerType.Namespace?.Contains(".Handlers.", StringComparison.OrdinalIgnoreCase) == true ||
                                   handlerType.Namespace?.EndsWith(".Handlers", StringComparison.OrdinalIgnoreCase) == true;

        if (!isInHandlersNamespace)
        {
            violations.Add(CreateViolation(
                handlerType,
                "Handlers should be placed in a Handlers namespace",
                $"Move '{handlerType.Name}' to a .Handlers namespace"));
        }
    }

    private void ValidateHandlerMediatRInterface(Type handlerType, List<RuleViolation> violations)
    {
        var implementsHandler = ImplementsGenericInterface(handlerType, typeof(IRequestHandler<,>)) ||
                               ImplementsGenericInterface(handlerType, typeof(IRequestHandler<>));

        if (!implementsHandler)
        {
            violations.Add(CreateViolation(
                handlerType,
                "Handlers must implement IRequestHandler<T> or IRequestHandler<T,R> from MediatR",
                $"Make '{handlerType.Name}' implement appropriate IRequestHandler interface"));
        }
    }

    private void ValidateHandlerMethods(Type handlerType, List<RuleViolation> violations)
    {
        var handleMethods = GetMethodsWithName(handlerType, "Handle");
        
        foreach (var method in handleMethods)
        {
            if (!IsAsyncMethod(method))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handler method '{method.Name}' should be async and return Task or Task<T>",
                    $"Make '{method.Name}' async and return Task or Task<T>"));
            }
            
            var parameters = method.GetParameters();
            if (parameters.Length > 0 && !parameters.Any(p => p.ParameterType == typeof(CancellationToken)))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handler method '{method.Name}' should accept CancellationToken parameter",
                    $"Add CancellationToken parameter to '{method.Name}' method"));
            }
        }
    }

    private static IEnumerable<System.Reflection.MethodInfo> GetMethodsWithName(Type type, string methodName) =>
        type.GetMethods().Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
}