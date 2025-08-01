using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.EventDriven;

/// <summary>
/// Rule to validate integration event patterns across bounded contexts and external systems.
/// </summary>
public sealed class IntegrationEventsRule : ArchitectureRuleBase
{
    public override string RuleId => "EVT002";
    public override string Name => "Integration Events Rule";
    public override string Description => "Validates that integration events are properly implemented and handled across boundaries";
    public override string Category => "EventDriven";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var integrationEventTypes = context.Types.Where(IsIntegrationEvent).ToList();
            var eventHandlerTypes = context.Types.Where(IsIntegrationEventHandler).ToList();
            var eventPublisherTypes = context.Types.Where(IsEventPublisher).ToList();
            var applicationServiceTypes = context.Types.Where(IsApplicationService).ToList();

            // Validate integration event implementations
            foreach (var eventType in integrationEventTypes)
            {
                ValidateIntegrationEventStructure(eventType, violations);
                ValidateIntegrationEventSerialization(eventType, violations);
                ValidateIntegrationEventVersioning(eventType, violations);
                ValidateIntegrationEventNaming(eventType, violations);
            }

            // Validate integration event handlers
            foreach (var handlerType in eventHandlerTypes)
            {
                ValidateIntegrationEventHandlerStructure(handlerType, violations);
                ValidateIntegrationEventHandlerIdempotency(handlerType, violations);
                ValidateIntegrationEventHandlerErrorHandling(handlerType, violations);
            }

            // Validate event publishers
            foreach (var publisherType in eventPublisherTypes)
            {
                ValidateEventPublisherReliability(publisherType, violations);
                ValidateEventPublisherConfiguration(publisherType, violations);
            }

            // Validate application service event publishing
            foreach (var serviceType in applicationServiceTypes)
            {
                ValidateApplicationServiceEventPublishing(serviceType, violations);
            }

        }, cancellationToken);

        return violations;
    }

    private static bool IsIntegrationEvent(Type type)
    {
        return type.Name.EndsWith("IntegrationEvent", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("ExternalEvent", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("IntegrationEvent", StringComparison.OrdinalIgnoreCase)) ||
               (IsInApplicationNamespace(type) && type.Name.Contains("Event", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsIntegrationEventHandler(Type type)
    {
        return type.Name.EndsWith("IntegrationEventHandler", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("EventHandler", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("EventHandler", StringComparison.OrdinalIgnoreCase)) ||
               type.GetMethods().Any(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase) &&
                                         m.GetParameters().Any(p => IsIntegrationEvent(p.ParameterType)));
    }

    private static bool IsEventPublisher(Type type)
    {
        return type.Name.Contains("EventPublisher", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("EventBus", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("MessageBus", StringComparison.OrdinalIgnoreCase) ||
               type.GetMethods().Any(m => m.Name.Contains("Publish", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsApplicationService(Type type)
    {
        return type.Name.EndsWith("ApplicationService", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("AppService", StringComparison.OrdinalIgnoreCase) ||
               (type.Name.EndsWith("Service", StringComparison.OrdinalIgnoreCase) && IsInApplicationNamespace(type));
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

    private void ValidateIntegrationEventStructure(Type eventType, List<RuleViolation> violations)
    {
        // Integration events should be in Application layer or shared contracts
        if (!IsInApplicationNamespace(eventType) && !IsInSharedNamespace(eventType))
        {
            violations.Add(CreateViolation(
                eventType,
                "Integration events should be in Application layer or shared contracts",
                $"Move '{eventType.Name}' to Application or Contracts namespace"));
        }

        // Integration events should be immutable
        var hasPublicSetters = eventType.GetProperties()
            .Any(p => p.CanWrite && p.SetMethod?.IsPublic == true);

        if (hasPublicSetters)
        {
            violations.Add(CreateViolation(
                eventType,
                "Integration events should be immutable for reliable serialization",
                "Use init-only setters or read-only properties"));
        }

        // Integration events should implement IIntegrationEvent or similar
        var implementsIntegrationEventInterface = eventType.GetInterfaces()
            .Any(i => i.Name.Contains("IntegrationEvent", StringComparison.OrdinalIgnoreCase) ||
                     i.Name.Contains("Event", StringComparison.OrdinalIgnoreCase));

        if (!implementsIntegrationEventInterface)
        {
            violations.Add(CreateViolation(
                eventType,
                "Integration events should implement IIntegrationEvent interface",
                $"Make '{eventType.Name}' implement IIntegrationEvent"));
        }
    }

    private void ValidateIntegrationEventSerialization(Type eventType, List<RuleViolation> violations)
    {
        var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Check for parameterless constructor (required for serialization)
        var hasParameterlessConstructor = eventType.GetConstructors()
            .Any(c => c.GetParameters().Length == 0);

        if (!hasParameterlessConstructor)
        {
            violations.Add(CreateViolation(
                eventType,
                "Integration events should have parameterless constructor for serialization",
                "Add parameterless constructor"));
        }

        // Check for complex types that might not serialize well
        foreach (var property in properties)
        {
            if (IsComplexType(property.PropertyType) && !HasSerializationSupport(property.PropertyType))
            {
                violations.Add(CreateViolation(
                    eventType,
                    $"Property '{property.Name}' uses complex type that may not serialize properly",
                    "Use simple types or ensure proper serialization attributes"));
            }
        }

        // Check for circular references
        ValidateForCircularReferences(eventType, violations, new HashSet<Type>());
    }

    private void ValidateIntegrationEventVersioning(Type eventType, List<RuleViolation> violations)
    {
        var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Should have version information
        var hasVersionProperty = properties.Any(p => 
            p.Name.Equals("Version", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Version", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("SchemaVersion", StringComparison.OrdinalIgnoreCase));

        if (!hasVersionProperty)
        {
            violations.Add(CreateViolation(
                eventType,
                "Integration events should have version information for schema evolution",
                "Add Version or SchemaVersion property"));
        }

        // Should have event type identifier
        var hasEventTypeProperty = properties.Any(p => 
            p.Name.Equals("EventType", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("Type", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("EventName", StringComparison.OrdinalIgnoreCase));

        if (!hasEventTypeProperty)
        {
            violations.Add(CreateViolation(
                eventType,
                "Integration events should have event type identifier",
                "Add EventType or Type property"));
        }
    }

    private void ValidateIntegrationEventNaming(Type eventType, List<RuleViolation> violations)
    {
        var name = eventType.Name;

        // Should use past tense and business language
        if (!IsPastTenseEventName(name))
        {
            violations.Add(CreateViolation(
                eventType,
                $"Integration event '{name}' should use past tense naming",
                "Use past tense business language"));
        }

        // Should be specific about the bounded context
        if (!HasContextualNaming(name))
        {
            violations.Add(CreateViolation(
                eventType,
                $"Integration event '{name}' should include bounded context information",
                "Include context information in event name"));
        }
    }

    private void ValidateIntegrationEventHandlerStructure(Type handlerType, List<RuleViolation> violations)
    {
        // Handlers should be in Application or Infrastructure layer
        if (!IsInApplicationNamespace(handlerType) && !IsInInfrastructureNamespace(handlerType))
        {
            violations.Add(CreateViolation(
                handlerType,
                "Integration event handlers should be in Application or Infrastructure layer",
                $"Move '{handlerType.Name}' to appropriate layer"));
        }

        // Handlers should have Handle method
        var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!handleMethods.Any())
        {
            violations.Add(CreateViolation(
                handlerType,
                "Integration event handlers should have Handle method",
                "Add Handle method"));
        }

        // Handle methods should be async
        foreach (var method in handleMethods)
        {
            if (!IsAsyncMethod(method))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handle method should be async for proper integration event processing",
                    "Make Handle method async"));
            }
        }
    }

    private void ValidateIntegrationEventHandlerIdempotency(Type handlerType, List<RuleViolation> violations)
    {
        var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in handleMethods)
        {
            // Check for idempotency considerations (this is a heuristic check)
            var methodName = method.Name;
            var parameters = method.GetParameters();

            // Should handle duplicate events gracefully
            var hasIdempotencyCheck = parameters.Any(p => 
                p.Name?.Contains("Id", StringComparison.OrdinalIgnoreCase) == true ||
                p.ParameterType.GetProperties().Any(prop => 
                    prop.Name.Contains("Id", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("CorrelationId", StringComparison.OrdinalIgnoreCase)));

            if (!hasIdempotencyCheck)
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Integration event handler should consider idempotency (handle duplicate events)",
                    "Implement idempotency checks using event IDs"));
            }
        }
    }

    private void ValidateIntegrationEventHandlerErrorHandling(Type handlerType, List<RuleViolation> violations)
    {
        var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in handleMethods)
        {
            // Methods should return Result type for proper error handling
            if (!ReturnsResultType(method) && method.ReturnType != typeof(Task))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Integration event handler should return Result type for error handling",
                    "Return Result or Result<T> for proper error handling"));
            }

            // Should handle failures gracefully (check for retry/dead letter patterns)
            // This is a naming convention check - in practice, you'd check for specific attributes or interfaces
            var methodBodyIndicatesErrorHandling = 
                method.GetCustomAttributes().Any(attr => 
                    attr.GetType().Name.Contains("Retry", StringComparison.OrdinalIgnoreCase) ||
                    attr.GetType().Name.Contains("DeadLetter", StringComparison.OrdinalIgnoreCase));

            if (!methodBodyIndicatesErrorHandling)
            {
                violations.Add(CreateViolation(
                    handlerType,
                    "Integration event handlers should have retry and dead letter handling",
                    "Implement retry policies and dead letter queue handling"));
            }
        }
    }

    private void ValidateEventPublisherReliability(Type publisherType, List<RuleViolation> violations)
    {
        var publishMethods = publisherType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Publish", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in publishMethods)
        {
            // Publish methods should be async
            if (!IsAsyncMethod(method))
            {
                violations.Add(CreateViolation(
                    publisherType,
                    $"Event publish method '{method.Name}' should be async",
                    "Make publish methods async"));
            }

            // Should return Result type for error handling
            if (!ReturnsResultType(method))
            {
                violations.Add(CreateViolation(
                    publisherType,
                    $"Event publish method '{method.Name}' should return Result type",
                    "Return Result type for proper error handling"));
            }

            // Should have cancellation token support
            var hasCancellationToken = method.GetParameters()
                .Any(p => p.ParameterType == typeof(CancellationToken));

            if (!hasCancellationToken)
            {
                violations.Add(CreateViolation(
                    publisherType,
                    $"Event publish method '{method.Name}' should support cancellation",
                    "Add CancellationToken parameter"));
            }
        }
    }

    private void ValidateEventPublisherConfiguration(Type publisherType, List<RuleViolation> violations)
    {
        // Event publishers should not have hard-coded configuration
        var fields = publisherType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        var properties = publisherType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var hasHardCodedConfig = fields.Any(f => 
            f.FieldType == typeof(string) && 
            (f.Name.Contains("Url", StringComparison.OrdinalIgnoreCase) ||
             f.Name.Contains("Connection", StringComparison.OrdinalIgnoreCase) ||
             f.Name.Contains("Queue", StringComparison.OrdinalIgnoreCase))) ||
            properties.Any(p => 
                p.PropertyType == typeof(string) && 
                p.CanWrite &&
                (p.Name.Contains("Url", StringComparison.OrdinalIgnoreCase) ||
                 p.Name.Contains("Connection", StringComparison.OrdinalIgnoreCase)));

        if (hasHardCodedConfig)
        {
            violations.Add(CreateViolation(
                publisherType,
                "Event publishers should use configuration injection, not hard-coded values",
                "Inject configuration through constructor or options pattern"));
        }
    }

    private void ValidateApplicationServiceEventPublishing(Type serviceType, List<RuleViolation> violations)
    {
        var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var hasEventPublishing = methods.Any(m => 
            m.GetParameters().Any(p => IsEventPublisher(p.ParameterType)) ||
            m.Name.Contains("Publish", StringComparison.OrdinalIgnoreCase));

        // Application services that orchestrate business operations should publish integration events
        var hasBusinessMethods = methods.Any(m => 
            !m.IsSpecialName && 
            m.DeclaringType == serviceType &&
            IsBusinessMethod(m));

        if (hasBusinessMethods && !hasEventPublishing)
        {
            violations.Add(CreateViolation(
                serviceType,
                "Application services should publish integration events for external communication",
                "Add integration event publishing capability"));
        }
    }

    private static bool IsInSharedNamespace(Type type)
    {
        return type.Namespace?.Contains(".Shared.", StringComparison.OrdinalIgnoreCase) == true ||
               type.Namespace?.Contains(".Contracts.", StringComparison.OrdinalIgnoreCase) == true ||
               type.Namespace?.EndsWith(".Shared", StringComparison.OrdinalIgnoreCase) == true ||
               type.Namespace?.EndsWith(".Contracts", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsComplexType(Type type)
    {
        return !type.IsPrimitive && 
               type != typeof(string) && 
               type != typeof(DateTime) && 
               type != typeof(DateTimeOffset) && 
               type != typeof(Guid) && 
               !type.IsEnum;
    }

    private static bool HasSerializationSupport(Type type)
    {
        return type.GetCustomAttributes().Any(attr => 
            attr.GetType().Name.Contains("Serializable", StringComparison.OrdinalIgnoreCase) ||
            attr.GetType().Name.Contains("JsonObject", StringComparison.OrdinalIgnoreCase) ||
            attr.GetType().Name.Contains("DataContract", StringComparison.OrdinalIgnoreCase));
    }

    private void ValidateForCircularReferences(Type type, List<RuleViolation> violations, HashSet<Type> visitedTypes)
    {
        if (visitedTypes.Contains(type))
        {
            violations.Add(CreateViolation(
                type,
                "Integration event has circular reference that may cause serialization issues",
                "Remove circular references or use serialization attributes"));
            return;
        }

        visitedTypes.Add(type);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (IsComplexType(property.PropertyType) && !property.PropertyType.IsGenericType)
            {
                ValidateForCircularReferences(property.PropertyType, violations, visitedTypes);
            }
        }

        visitedTypes.Remove(type);
    }

    private static bool IsPastTenseEventName(string eventName)
    {
        string[] pastTenseSuffixes = { "ed", "Created", "Updated", "Deleted", "Processed", "Completed", "Started", "Finished", "Approved", "Rejected", "Cancelled", "Published", "Received" };
        return pastTenseSuffixes.Any(suffix => eventName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasContextualNaming(string eventName)
    {
        // Simple heuristic - should contain context information
        // In practice, you might maintain a list of known bounded contexts
        return eventName.Length > 10; // Very basic check - replace with actual context validation
    }

    private static bool IsAsyncMethod(MethodInfo method)
    {
        return method.ReturnType == typeof(Task) || 
               (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
    }

    private static bool ReturnsResultType(MethodInfo method)
    {
        return method.ReturnType.Name.StartsWith("Result", StringComparison.OrdinalIgnoreCase) ||
               method.ReturnType.Namespace?.Contains("Result") == true;
    }

    private static bool IsBusinessMethod(MethodInfo method)
    {
        return !method.IsSpecialName && 
               method.IsPublic && 
               !method.Name.StartsWith("get_") && 
               !method.Name.StartsWith("set_") &&
               method.DeclaringType == method.ReflectedType;
    }
}