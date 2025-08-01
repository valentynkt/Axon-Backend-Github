using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.EventDriven;

/// <summary>
/// Rule to validate domain event publishing and handling patterns within aggregates and domain services.
/// </summary>
public sealed class DomainEventsRule : ArchitectureRuleBase
{
    public override string RuleId => "EVT001";
    public override string Name => "Domain Events Rule";
    public override string Description => "Validates that domain events are properly implemented, published, and handled";
    public override string Category => "EventDriven";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var domainEventTypes = context.Types.Where(IsDomainEvent).ToList();
            var aggregateTypes = context.Types.Where(IsAggregateRoot).ToList();
            var domainServiceTypes = context.Types.Where(IsDomainService).ToList();

            // Validate domain event implementations
            foreach (var eventType in domainEventTypes)
            {
                ValidateDomainEventStructure(eventType, violations);
                ValidateDomainEventImmutability(eventType, violations);
                ValidateDomainEventNaming(eventType, violations);
                ValidateDomainEventProperties(eventType, violations);
            }

            // Validate aggregate domain event publishing
            foreach (var aggregateType in aggregateTypes)
            {
                ValidateAggregateDomainEventSupport(aggregateType, violations);
                ValidateAggregateEventPublishing(aggregateType, violations);
            }

            // Validate domain service event handling
            foreach (var serviceType in domainServiceTypes)
            {
                ValidateDomainServiceEventHandling(serviceType, violations);
            }

        }, cancellationToken);

        return violations;
    }

    private static bool IsDomainEvent(Type type)
    {
        return type.Name.EndsWith("Event", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("DomainEvent", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("DomainEvent", StringComparison.OrdinalIgnoreCase)) ||
               (IsInDomainNamespace(type) && type.Name.Contains("Event", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAggregateRoot(Type type)
    {
        return InheritsFromAggregateRoot(type) || 
               type.Name.EndsWith("Aggregate", StringComparison.OrdinalIgnoreCase) ||
               (IsInDomainNamespace(type) && type.IsClass && !type.IsAbstract);
    }

    private static bool IsDomainService(Type type)
    {
        return type.Name.EndsWith("DomainService", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("Service", StringComparison.OrdinalIgnoreCase) && IsInDomainNamespace(type);
    }

    private static bool InheritsFromAggregateRoot(Type type)
    {
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.Name.Contains("AggregateRoot", StringComparison.OrdinalIgnoreCase))
                return true;
            baseType = baseType.BaseType;
        }
        return false;
    }

    private static bool IsInDomainNamespace(Type type)
    {
        return type.Namespace?.Contains(".Domain.", StringComparison.OrdinalIgnoreCase) == true ||
               type.Namespace?.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase) == true;
    }

    private void ValidateDomainEventStructure(Type eventType, List<RuleViolation> violations)
    {
        // Domain events should be records or immutable classes
        if (!eventType.IsValueType && !IsRecord(eventType))
        {
            var hasSetters = eventType.GetProperties()
                .Any(p => p.CanWrite && p.SetMethod?.IsPublic == true);

            if (hasSetters)
            {
                violations.Add(CreateViolation(
                    eventType,
                    "Domain events should be immutable (consider using records or read-only properties)",
                    "Convert to record or make all properties read-only"));
            }
        }

        // Domain events should be in Domain namespace
        if (!IsInDomainNamespace(eventType))
        {
            violations.Add(CreateViolation(
                eventType,
                "Domain events must be placed in the Domain layer",
                $"Move '{eventType.Name}' to a Domain namespace"));
        }

        // Domain events should implement IDomainEvent or similar interface
        var implementsDomainEventInterface = eventType.GetInterfaces()
            .Any(i => i.Name.Contains("DomainEvent", StringComparison.OrdinalIgnoreCase) ||
                     i.Name.Contains("Event", StringComparison.OrdinalIgnoreCase));

        if (!implementsDomainEventInterface)
        {
            violations.Add(CreateViolation(
                eventType,
                "Domain events should implement IDomainEvent interface",
                $"Make '{eventType.Name}' implement IDomainEvent"));
        }
    }

    private void ValidateDomainEventImmutability(Type eventType, List<RuleViolation> violations)
    {
        var mutableProperties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.SetMethod?.IsPublic == true)
            .ToList();

        foreach (var property in mutableProperties)
        {
            violations.Add(CreateViolation(
                eventType,
                $"Domain event property '{property.Name}' should be immutable",
                "Use init-only setters or make property read-only"));
        }

        // Check for public fields (should not exist in events)
        var publicFields = eventType.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in publicFields)
        {
            violations.Add(CreateViolation(
                eventType,
                $"Domain event should not have public fields like '{field.Name}'",
                "Convert public fields to properties"));
        }
    }

    private void ValidateDomainEventNaming(Type eventType, List<RuleViolation> violations)
    {
        var name = eventType.Name;

        // Events should use past tense naming
        if (!IsPastTenseEventName(name))
        {
            violations.Add(CreateViolation(
                eventType,
                $"Domain event '{name}' should use past tense naming (e.g., OrderCreated, PaymentProcessed)",
                "Rename to use past tense"));
        }

        // Should not have generic names
        string[] genericNames = { "Event", "DomainEvent", "Message" };
        if (genericNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            violations.Add(CreateViolation(
                eventType,
                $"Domain event '{name}' should have specific business meaning",
                "Use specific business event names"));
        }
    }

    private void ValidateDomainEventProperties(Type eventType, List<RuleViolation> violations)
    {
        var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Should have an Id or EventId property
        var hasIdProperty = properties.Any(p => 
            p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("EventId", StringComparison.OrdinalIgnoreCase));

        if (!hasIdProperty)
        {
            violations.Add(CreateViolation(
                eventType,
                "Domain events should have an Id or EventId property",
                "Add Id or EventId property"));
        }

        // Should have timestamp information
        var hasTimestamp = properties.Any(p => 
            p.Name.Contains("Timestamp", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("DateTime", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("OccurredAt", StringComparison.OrdinalIgnoreCase) ||
            p.PropertyType == typeof(DateTime) ||
            p.PropertyType == typeof(DateTimeOffset));

        if (!hasTimestamp)
        {
            violations.Add(CreateViolation(
                eventType,
                "Domain events should have timestamp information",
                "Add OccurredAt or Timestamp property"));
        }
    }

    private void ValidateAggregateDomainEventSupport(Type aggregateType, List<RuleViolation> violations)
    {
        var methods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var properties = aggregateType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Check for domain event collection or similar
        var hasDomainEventSupport = properties.Any(p => 
            p.Name.Contains("DomainEvent", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Event", StringComparison.OrdinalIgnoreCase)) ||
            methods.Any(m => 
                m.Name.Contains("AddEvent", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("RaiseEvent", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("PublishEvent", StringComparison.OrdinalIgnoreCase));

        var hasBusinessMethods = methods.Any(m => 
            !m.IsSpecialName && 
            m.DeclaringType == aggregateType && 
            !IsPropertyAccessor(m));

        if (hasBusinessMethods && !hasDomainEventSupport)
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Aggregates with business methods should support domain events",
                "Add domain event publishing capability"));
        }
    }

    private void ValidateAggregateEventPublishing(Type aggregateType, List<RuleViolation> violations)
    {
        var businessMethods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == aggregateType && !IsPropertyAccessor(m))
            .ToList();

        foreach (var method in businessMethods)
        {
            // Business methods that change state should potentially raise events
            if (method.ReturnType == typeof(void) || ReturnsResultType(method))
            {
                var methodBody = method.GetMethodBody();
                // Note: We can't easily inspect method body in reflection, 
                // so we'll check for naming patterns that suggest event publishing
                var methodName = method.Name;
                
                if (IsStateChangingMethod(methodName))
                {
                    // This is a heuristic - in a real implementation, you might use
                    // more sophisticated analysis or require specific patterns
                    violations.Add(CreateViolation(
                        aggregateType,
                        $"State-changing method '{methodName}' should consider raising domain events",
                        "Add domain event publishing to business methods"));
                }
            }
        }
    }

    private void ValidateDomainServiceEventHandling(Type serviceType, List<RuleViolation> violations)
    {
        var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        
        // Check for event handling methods
        var eventHandlerMethods = methods.Where(m => 
            m.Name.Contains("Handle", StringComparison.OrdinalIgnoreCase) &&
            m.GetParameters().Any(p => IsDomainEvent(p.ParameterType)))
            .ToList();

        foreach (var method in eventHandlerMethods)
        {
            // Event handlers should be async
            if (!IsAsyncMethod(method))
            {
                violations.Add(CreateViolation(
                    serviceType,
                    $"Domain event handler '{method.Name}' should be async",
                    "Make event handlers async"));
            }

            // Event handlers should have proper error handling
            if (!ReturnsResultType(method) && method.ReturnType != typeof(Task))
            {
                violations.Add(CreateViolation(
                    serviceType,
                    $"Domain event handler '{method.Name}' should return Result type for error handling",
                    "Return Result or Result<T> from event handlers"));
            }
        }
    }

    private static bool IsRecord(Type type)
    {
        return type.GetMethods().Any(m => m.Name == "<Clone>$" || m.Name.Contains("EqualityContract"));
    }

    private static bool IsPastTenseEventName(string eventName)
    {
        // Simple heuristic for past tense - ends with common past tense suffixes
        string[] pastTenseSuffixes = { "ed", "Created", "Updated", "Deleted", "Processed", "Completed", "Started", "Finished", "Approved", "Rejected", "Cancelled" };
        return pastTenseSuffixes.Any(suffix => eventName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPropertyAccessor(MethodInfo method)
    {
        return method.IsSpecialName && (method.Name.StartsWith("get_") || method.Name.StartsWith("set_"));
    }

    private static bool ReturnsResultType(MethodInfo method)
    {
        return method.ReturnType.Name.StartsWith("Result", StringComparison.OrdinalIgnoreCase) ||
               method.ReturnType.Namespace?.Contains("Result") == true;
    }

    private static bool IsAsyncMethod(MethodInfo method)
    {
        return method.ReturnType == typeof(Task) || 
               (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
    }

    private static bool IsStateChangingMethod(string methodName)
    {
        string[] stateChangingVerbs = { "Create", "Update", "Delete", "Process", "Complete", "Start", "Finish", "Approve", "Reject", "Cancel", "Add", "Remove", "Change", "Modify" };
        return stateChangingVerbs.Any(verb => methodName.StartsWith(verb, StringComparison.OrdinalIgnoreCase));
    }
}