using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.EventDriven;

/// <summary>
/// Rule to validate event handler implementation patterns for both domain and integration events.
/// </summary>
public sealed class EventHandlersRule : ArchitectureRuleBase
{
    public override string RuleId => "EVT003";
    public override string Name => "Event Handlers Rule";
    public override string Description => "Validates that event handlers are properly implemented with correct patterns";
    public override string Category => "EventDriven";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var eventHandlerTypes = context.Types.Where(IsEventHandler).ToList();
            var domainEventHandlerTypes = context.Types.Where(IsDomainEventHandler).ToList();
            var integrationEventHandlerTypes = context.Types.Where(IsIntegrationEventHandler).ToList();

            // Validate all event handlers
            foreach (var handlerType in eventHandlerTypes)
            {
                ValidateEventHandlerStructure(handlerType, violations);
                ValidateEventHandlerNaming(handlerType, violations);
                ValidateEventHandlerLifetime(handlerType, violations);
                ValidateEventHandlerDependencies(handlerType, violations);
                ValidateEventHandlerMethods(handlerType, violations);
            }

            // Validate domain event handlers specifically
            foreach (var handlerType in domainEventHandlerTypes)
            {
                ValidateDomainEventHandlerPlacement(handlerType, violations);
                ValidateDomainEventHandlerTransactionality(handlerType, violations);
            }

            // Validate integration event handlers specifically
            foreach (var handlerType in integrationEventHandlerTypes)
            {
                ValidateIntegrationEventHandlerPlacement(handlerType, violations);
                ValidateIntegrationEventHandlerIdempotency(handlerType, violations);
                ValidateIntegrationEventHandlerResilience(handlerType, violations);
            }

        }, cancellationToken);

        return violations;
    }

    private static bool IsEventHandler(Type type)
    {
        return type.Name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("EventHandler", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("Handler", StringComparison.OrdinalIgnoreCase)) ||
               type.GetMethods().Any(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDomainEventHandler(Type type)
    {
        return IsEventHandler(type) &&
               (type.Name.Contains("Domain", StringComparison.OrdinalIgnoreCase) ||
                IsInDomainNamespace(type) ||
                type.GetMethods().Any(m => m.GetParameters()
                    .Any(p => IsDomainEvent(p.ParameterType))));
    }

    private static bool IsIntegrationEventHandler(Type type)
    {
        return IsEventHandler(type) &&
               (type.Name.Contains("Integration", StringComparison.OrdinalIgnoreCase) ||
                type.Name.Contains("External", StringComparison.OrdinalIgnoreCase) ||
                type.GetMethods().Any(m => m.GetParameters()
                    .Any(p => IsIntegrationEvent(p.ParameterType))));
    }

    private static bool IsDomainEvent(Type type)
    {
        return type.Name.EndsWith("Event", StringComparison.OrdinalIgnoreCase) &&
               IsInDomainNamespace(type);
    }

    private static bool IsIntegrationEvent(Type type)
    {
        return type.Name.EndsWith("IntegrationEvent", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("ExternalEvent", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInDomainNamespace(Type type)
    {
        return type.Namespace?.Contains(".Domain.", StringComparison.OrdinalIgnoreCase) == true ||
               type.Namespace?.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsInApplicationNamespace(Type type)
    {
        return type.Namespace?.Contains(".Application.", StringComparison.OrdinalIgnoreCase) == true ||
               type.Namespace?.EndsWith(".Application", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsInInfrastructureNamespace(Type type)
    {
        return type.Namespace?.Contains(".Infrastructure.", StringComparison.OrdinalIgnoreCase) == true ||
               type.Namespace?.EndsWith(".Infrastructure", StringComparison.OrdinalIgnoreCase) == true;
    }

    private void ValidateEventHandlerStructure(Type handlerType, List<RuleViolation> violations)
    {
        // Event handlers should be sealed to prevent inheritance issues
        if (!handlerType.IsSealed && !handlerType.IsAbstract)
        {
            violations.Add(CreateViolation(
                handlerType,
                "Event handlers should be sealed to prevent inheritance issues",
                $"Make '{handlerType.Name}' sealed"));
        }

        // Event handlers should implement proper interfaces
        var implementsHandlerInterface = handlerType.GetInterfaces()
            .Any(i => i.Name.Contains("Handler", StringComparison.OrdinalIgnoreCase) ||
                     i.Name.Contains("INotificationHandler", StringComparison.OrdinalIgnoreCase) ||
                     i.Name.Contains("IEventHandler", StringComparison.OrdinalIgnoreCase));

        if (!implementsHandlerInterface)
        {
            violations.Add(CreateViolation(
                handlerType,
                "Event handlers should implement proper handler interfaces",
                $"Make '{handlerType.Name}' implement IEventHandler or INotificationHandler"));
        }

        // Should have Handle method
        var hasMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!hasMethods.Any())
        {
            violations.Add(CreateViolation(
                handlerType,
                "Event handlers must have Handle method",
                "Add Handle method to event handler"));
        }
    }

    private void ValidateEventHandlerNaming(Type handlerType, List<RuleViolation> violations)
    {
        var name = handlerType.Name;

        // Should end with Handler
        if (!name.EndsWith("Handler", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                handlerType,
                "Event handlers should end with 'Handler' suffix",
                $"Rename '{name}' to include Handler suffix"));
        }

        // Should indicate what event it handles
        if (!HasDescriptiveHandlerName(name))
        {
            violations.Add(CreateViolation(
                handlerType,
                $"Event handler '{name}' should indicate what event it handles",
                "Use descriptive names like OrderCreatedHandler"));
        }

        // Should not be generic handler name
        string[] genericNames = { "Handler", "EventHandler", "BaseHandler" };
        if (genericNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                handlerType,
                $"Event handler should not use generic name '{name}'",
                "Use specific business-focused handler names"));
        }
    }

    private void ValidateEventHandlerLifetime(Type handlerType, List<RuleViolation> violations)
    {
        // Check for singleton attributes or patterns (handlers should typically be transient)
        var attributes = handlerType.GetCustomAttributes();
        var hasSingletonAttribute = attributes.Any(attr => 
            attr.GetType().Name.Contains("Singleton", StringComparison.OrdinalIgnoreCase));

        if (hasSingletonAttribute)
        {
            violations.Add(CreateViolation(
                handlerType,
                "Event handlers should not be singletons due to potential state issues",
                "Use transient or scoped lifetime for event handlers"));
        }

        // Check for instance fields that could cause state issues
        var instanceFields = handlerType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => !f.IsInitOnly && !f.IsLiteral)
            .ToList();

        if (instanceFields.Any(f => !IsAllowedField(f)))
        {
            violations.Add(CreateViolation(
                handlerType,
                "Event handlers should be stateless - avoid mutable instance fields",
                "Remove mutable fields or use dependency injection"));
        }
    }

    private void ValidateEventHandlerDependencies(Type handlerType, List<RuleViolation> violations)
    {
        var constructors = handlerType.GetConstructors();
        var primaryConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();

        if (primaryConstructor != null)
        {
            var parameters = primaryConstructor.GetParameters();

            // Should not depend on UI or Web concerns
            foreach (var param in parameters)
            {
                if (IsUIOrWebType(param.ParameterType))
                {
                    violations.Add(CreateViolation(
                        handlerType,
                        $"Event handler should not depend on UI/Web type '{param.ParameterType.Name}'",
                        "Event handlers should be independent of UI concerns"));
                }
            }

            // Should not have too many dependencies
            if (parameters.Length > 5)
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Event handler has too many dependencies ({parameters.Length})",
                    "Consider reducing dependencies or refactoring"));
            }
        }
    }

    private void ValidateEventHandlerMethods(Type handlerType, List<RuleViolation> violations)
    {
        var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in handleMethods)
        {
            // Handle methods should be async
            if (!IsAsyncMethod(method))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handle method should be async for proper event processing",
                    "Make Handle method async"));
            }

            // Should have proper return type
            if (!ReturnsTaskOrResult(method))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handle method should return Task or Result type",
                    "Return Task, Task<Result>, or Result type"));
            }

            // Should have cancellation token support
            var hasCancellationToken = method.GetParameters()
                .Any(p => p.ParameterType == typeof(CancellationToken));

            if (!hasCancellationToken)
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handle method should support cancellation",
                    "Add CancellationToken parameter"));
            }

            // Should handle only one event type
            var eventParameters = method.GetParameters()
                .Where(p => IsEventType(p.ParameterType))
                .ToList();

            if (eventParameters.Count > 1)
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handle method should handle only one event type",
                    "Create separate handlers for different event types"));
            }

            if (eventParameters.Count == 0)
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handle method should have an event parameter",
                    "Add event parameter to Handle method"));
            }
        }

        // Should have only Handle methods (no other business logic)
        var otherPublicMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase) &&
                       !m.IsSpecialName &&
                       m.DeclaringType == handlerType)
            .ToList();

        if (otherPublicMethods.Any())
        {
            violations.Add(CreateViolation(
                handlerType,
                "Event handlers should only contain Handle methods",
                "Move other business logic to separate services"));
        }
    }

    private void ValidateDomainEventHandlerPlacement(Type handlerType, List<RuleViolation> violations)
    {
        // Domain event handlers should be in Domain or Application layer
        if (!IsInDomainNamespace(handlerType) && !IsInApplicationNamespace(handlerType))
        {
            violations.Add(CreateViolation(
                handlerType,
                "Domain event handlers should be in Domain or Application layer",
                $"Move '{handlerType.Name}' to appropriate layer"));
        }
    }

    private void ValidateDomainEventHandlerTransactionality(Type handlerType, List<RuleViolation> violations)
    {
        // Domain event handlers should be aware of transaction boundaries
        var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in handleMethods)
        {
            // Check for transaction attributes or patterns
            var hasTransactionAttribute = method.GetCustomAttributes()
                .Any(attr => attr.GetType().Name.Contains("Transaction", StringComparison.OrdinalIgnoreCase));

            var dependsOnTransactionalServices = method.DeclaringType?.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Any(p => IsTransactionalService(p.ParameterType)) ?? false;

            if (!hasTransactionAttribute && !dependsOnTransactionalServices)
            {
                violations.Add(CreateViolation(
                    handlerType,
                    "Domain event handlers should consider transaction boundaries",
                    "Use transaction attributes or inject transactional services"));
            }
        }
    }

    private void ValidateIntegrationEventHandlerPlacement(Type handlerType, List<RuleViolation> violations)
    {
        // Integration event handlers should be in Application or Infrastructure layer
        if (!IsInApplicationNamespace(handlerType) && !IsInInfrastructureNamespace(handlerType))
        {
            violations.Add(CreateViolation(
                handlerType,
                "Integration event handlers should be in Application or Infrastructure layer",
                $"Move '{handlerType.Name}' to appropriate layer"));
        }
    }

    private void ValidateIntegrationEventHandlerIdempotency(Type handlerType, List<RuleViolation> violations)
    {
        var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in handleMethods)
        {
            // Should handle duplicate events gracefully
            var eventParameters = method.GetParameters()
                .Where(p => IsEventType(p.ParameterType))
                .ToList();

            foreach (var eventParam in eventParameters)
            {
                var eventType = eventParam.ParameterType;
                var hasIdProperty = eventType.GetProperties()
                    .Any(p => p.Name.Contains("Id", StringComparison.OrdinalIgnoreCase));

                if (!hasIdProperty)
                {
                    violations.Add(CreateViolation(
                        handlerType,
                        "Integration events should have ID for idempotency checks",
                        "Ensure events have unique identifiers"));
                }
            }

            // Check for idempotency implementation (heuristic)
            var parameters = method.GetParameters();
            var dependsOnIdempotencyService = handlerType.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Any(p => IsIdempotencyService(p.ParameterType));

            if (!dependsOnIdempotencyService)
            {
                violations.Add(CreateViolation(
                    handlerType,
                    "Integration event handlers should implement idempotency checks",
                    "Inject and use idempotency service or repository"));
            }
        }
    }

    private void ValidateIntegrationEventHandlerResilience(Type handlerType, List<RuleViolation> violations)
    {
        var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in handleMethods)
        {
            // Should have retry attributes or resilience patterns
            var hasResilienceAttribute = method.GetCustomAttributes()
                .Any(attr => 
                    attr.GetType().Name.Contains("Retry", StringComparison.OrdinalIgnoreCase) ||
                    attr.GetType().Name.Contains("Circuit", StringComparison.OrdinalIgnoreCase) ||
                    attr.GetType().Name.Contains("Resilience", StringComparison.OrdinalIgnoreCase));

            if (!hasResilienceAttribute)
            {
                violations.Add(CreateViolation(
                    handlerType,
                    "Integration event handlers should have resilience patterns",
                    "Add retry policies or circuit breakers"));
            }

            // Should handle failures gracefully
            if (!ReturnsTaskOrResult(method))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    "Integration event handlers should return Result type for error handling",
                    "Return Result type to handle failures properly"));
            }
        }
    }

    private static bool HasDescriptiveHandlerName(string handlerName)
    {
        // Should contain more than just "Handler" - should indicate what it handles
        var nameWithoutHandler = handlerName.Replace("Handler", "", StringComparison.OrdinalIgnoreCase);
        return nameWithoutHandler.Length > 3; // Very basic check
    }

    private static bool IsAllowedField(FieldInfo field)
    {
        // Allow readonly fields and dependency injection fields
        return field.IsInitOnly || 
               field.FieldType.IsInterface ||
               field.FieldType.Name.Contains("Service", StringComparison.OrdinalIgnoreCase) ||
               field.FieldType.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase) ||
               field.FieldType.Name.Contains("Logger", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUIOrWebType(Type type)
    {
        return type.Namespace?.Contains("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase) == true ||
               type.Namespace?.Contains("System.Web", StringComparison.OrdinalIgnoreCase) == true ||
               type.Name.Contains("Controller", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("HttpContext", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAsyncMethod(MethodInfo method)
    {
        return method.ReturnType == typeof(Task) || 
               (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
    }

    private static bool ReturnsTaskOrResult(MethodInfo method)
    {
        return IsAsyncMethod(method) ||
               method.ReturnType.Name.StartsWith("Result", StringComparison.OrdinalIgnoreCase) ||
               method.ReturnType.Namespace?.Contains("Result") == true;
    }

    private static bool IsEventType(Type type)
    {
        return type.Name.Contains("Event", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("Event", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTransactionalService(Type type)
    {
        return type.Name.Contains("UnitOfWork", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Transaction", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("DbContext", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIdempotencyService(Type type)
    {
        return type.Name.Contains("Idempotency", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Cache", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("EventStore", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("DeduplicationService", StringComparison.OrdinalIgnoreCase);
    }
}