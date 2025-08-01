using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.EventDriven;

/// <summary>
/// Rule to validate event store usage patterns and implementations for event persistence.
/// </summary>
public sealed class EventStoreRule : ArchitectureRuleBase
{
    public override string RuleId => "EVT004";
    public override string Name => "Event Store Rule";
    public override string Description => "Validates that event stores are properly implemented and used for event persistence";
    public override string Category => "EventDriven";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var eventStoreTypes = context.Types.Where(IsEventStore).ToList();
            var eventRepositoryTypes = context.Types.Where(IsEventRepository).ToList();
            var eventTypes = context.Types.Where(IsStorableEvent).ToList();
            var aggregateTypes = context.Types.Where(IsAggregateRoot).ToList();

            // Validate event store implementations
            foreach (var storeType in eventStoreTypes)
            {
                ValidateEventStoreStructure(storeType, violations);
                ValidateEventStoreInterface(storeType, violations);
                ValidateEventStoreMethods(storeType, violations);
                ValidateEventStoreConcurrency(storeType, violations);
                ValidateEventStorePerformance(storeType, violations);
            }

            // Validate event repositories
            foreach (var repoType in eventRepositoryTypes)
            {
                ValidateEventRepositoryStructure(repoType, violations);
                ValidateEventRepositoryMethods(repoType, violations);
            }

            // Validate storable events
            foreach (var eventType in eventTypes)
            {
                ValidateEventStorability(eventType, violations);
                ValidateEventVersioning(eventType, violations);
                ValidateEventMetadata(eventType, violations);
            }

            // Validate aggregate event store usage
            foreach (var aggregateType in aggregateTypes)
            {
                ValidateAggregateEventStoreIntegration(aggregateType, violations);
            }

        }, cancellationToken);

        return violations;
    }

    private static bool IsEventStore(Type type)
    {
        return type.Name.Contains("EventStore", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("EventRepository", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("EventStore", StringComparison.OrdinalIgnoreCase)) ||
               type.GetMethods().Any(m => 
                   m.Name.Contains("SaveEvent", StringComparison.OrdinalIgnoreCase) ||
                   m.Name.Contains("AppendEvent", StringComparison.OrdinalIgnoreCase) ||
                   m.Name.Contains("GetEvents", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEventRepository(Type type)
    {
        return type.Name.EndsWith("EventRepository", StringComparison.OrdinalIgnoreCase) ||
               (type.Name.EndsWith("Repository", StringComparison.OrdinalIgnoreCase) &&
                type.Name.Contains("Event", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStorableEvent(Type type)
    {
        return type.Name.EndsWith("Event", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("Event", StringComparison.OrdinalIgnoreCase)) ||
               type.Name.Contains("Event", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAggregateRoot(Type type)
    {
        return InheritsFromAggregateRoot(type) || 
               type.Name.EndsWith("Aggregate", StringComparison.OrdinalIgnoreCase) ||
               (IsInDomainNamespace(type) && type.IsClass && !type.IsAbstract);
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

    private static bool IsInInfrastructureNamespace(Type type)
    {
        return type.Namespace?.Contains(".Infrastructure.", StringComparison.OrdinalIgnoreCase) == true ||
               type.Namespace?.EndsWith(".Infrastructure", StringComparison.OrdinalIgnoreCase) == true;
    }

    private void ValidateEventStoreStructure(Type storeType, List<RuleViolation> violations)
    {
        // Event stores should be in Infrastructure layer
        if (!IsInInfrastructureNamespace(storeType))
        {
            violations.Add(CreateViolation(
                storeType,
                "Event stores should be in Infrastructure layer",
                $"Move '{storeType.Name}' to Infrastructure namespace"));
        }

        // Event stores should implement proper interfaces
        var implementsEventStoreInterface = storeType.GetInterfaces()
            .Any(i => i.Name.Contains("EventStore", StringComparison.OrdinalIgnoreCase) ||
                     i.Name.Contains("IEventStore", StringComparison.OrdinalIgnoreCase));

        if (!implementsEventStoreInterface)
        {
            violations.Add(CreateViolation(
                storeType,
                "Event stores should implement IEventStore interface",
                $"Make '{storeType.Name}' implement IEventStore"));
        }

        // Should not expose DbContext directly
        var properties = storeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var exposesDbContext = properties.Any(p => 
            p.PropertyType.Name.Contains("DbContext", StringComparison.OrdinalIgnoreCase));

        if (exposesDbContext)
        {
            violations.Add(CreateViolation(
                storeType,
                "Event stores should not expose DbContext publicly",
                "Encapsulate database context within the store"));
        }
    }

    private void ValidateEventStoreInterface(Type storeType, List<RuleViolation> violations)
    {
        var publicMethods = storeType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == storeType)
            .ToList();

        // Should have SaveEvents or AppendEvents method
        var hasSaveMethod = publicMethods.Any(m => 
            m.Name.Contains("Save", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Append", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Store", StringComparison.OrdinalIgnoreCase));

        if (!hasSaveMethod)
        {
            violations.Add(CreateViolation(
                storeType,
                "Event store should have SaveEvents or AppendEvents method",
                "Add event saving capability"));
        }

        // Should have GetEvents method
        var hasGetMethod = publicMethods.Any(m => 
            m.Name.Contains("Get", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Load", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Read", StringComparison.OrdinalIgnoreCase));

        if (!hasGetMethod)
        {
            violations.Add(CreateViolation(
                storeType,
                "Event store should have GetEvents method",
                "Add event retrieval capability"));
        }

        // Should have stream-based methods
        var hasStreamMethods = publicMethods.Any(m => 
            m.Name.Contains("Stream", StringComparison.OrdinalIgnoreCase) ||
            m.GetParameters().Any(p => p.Name?.Contains("Stream", StringComparison.OrdinalIgnoreCase) == true));

        if (!hasStreamMethods)
        {
            violations.Add(CreateViolation(
                storeType,
                "Event store should support stream-based operations",
                "Add stream-based event operations"));
        }
    }

    private void ValidateEventStoreMethods(Type storeType, List<RuleViolation> violations)
    {
        var publicMethods = storeType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == storeType)
            .ToList();

        foreach (var method in publicMethods)
        {
            // Event store methods should be async
            if (!IsAsyncMethod(method))
            {
                violations.Add(CreateViolation(
                    storeType,
                    $"Event store method '{method.Name}' should be async",
                    "Make event store methods async"));
            }

            // Should have cancellation token support
            var hasCancellationToken = method.GetParameters()
                .Any(p => p.ParameterType == typeof(CancellationToken));

            if (!hasCancellationToken)
            {
                violations.Add(CreateViolation(
                    storeType,
                    $"Event store method '{method.Name}' should support cancellation",
                    "Add CancellationToken parameter"));
            }

            // Should return Result type for error handling
            if (!ReturnsResultType(method) && method.ReturnType != typeof(Task))
            {
                violations.Add(CreateViolation(
                    storeType,
                    $"Event store method '{method.Name}' should return Result type",
                    "Return Result type for proper error handling"));
            }

            // Save/Append methods should handle version conflicts
            if (IsSaveMethod(method.Name))
            {
                var hasVersionParameter = method.GetParameters()
                    .Any(p => p.Name?.Contains("Version", StringComparison.OrdinalIgnoreCase) == true ||
                             p.Name?.Contains("ExpectedVersion", StringComparison.OrdinalIgnoreCase) == true);

                if (!hasVersionParameter)
                {
                    violations.Add(CreateViolation(
                        storeType,
                        $"Save method '{method.Name}' should handle optimistic concurrency",
                        "Add expected version parameter"));
                }
            }
        }
    }

    private void ValidateEventStoreConcurrency(Type storeType, List<RuleViolation> violations)
    {
        var saveMethods = storeType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => IsSaveMethod(m.Name))
            .ToList();

        foreach (var method in saveMethods)
        {
            // Should handle optimistic concurrency control
            var hasVersionHandling = method.GetParameters()
                .Any(p => p.Name?.Contains("Version", StringComparison.OrdinalIgnoreCase) == true) ||
                method.Name.Contains("ExpectedVersion", StringComparison.OrdinalIgnoreCase);

            if (!hasVersionHandling)
            {
                violations.Add(CreateViolation(
                    storeType,
                    $"Save method '{method.Name}' should implement optimistic concurrency control",
                    "Add version checking to prevent concurrent modification"));
            }

            // Should throw or return appropriate exceptions for conflicts
            var returnType = method.ReturnType;
            var handlesConflicts = returnType.Name.Contains("Result", StringComparison.OrdinalIgnoreCase) ||
                                 method.GetCustomAttributes().Any(attr => 
                                     attr.GetType().Name.Contains("Throws", StringComparison.OrdinalIgnoreCase));

            if (!handlesConflicts)
            {
                violations.Add(CreateViolation(
                    storeType,
                    $"Save method '{method.Name}' should handle version conflicts",
                    "Handle concurrency conflicts with appropriate results"));
            }
        }
    }

    private void ValidateEventStorePerformance(Type storeType, List<RuleViolation> violations)
    {
        var getMethods = storeType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => IsGetMethod(m.Name))
            .ToList();

        foreach (var method in getMethods)
        {
            // Should support pagination for large event streams
            var supportsPagination = method.GetParameters()
                .Any(p => p.Name?.Contains("Skip", StringComparison.OrdinalIgnoreCase) == true ||
                         p.Name?.Contains("Take", StringComparison.OrdinalIgnoreCase) == true ||
                         p.Name?.Contains("Limit", StringComparison.OrdinalIgnoreCase) == true ||
                         p.Name?.Contains("PageSize", StringComparison.OrdinalIgnoreCase) == true);

            if (!supportsPagination)
            {
                violations.Add(CreateViolation(
                    storeType,
                    $"Get method '{method.Name}' should support pagination",
                    "Add pagination parameters to prevent loading large datasets"));
            }

            // Should support filtering by version range
            var supportsVersionFiltering = method.GetParameters()
                .Any(p => p.Name?.Contains("FromVersion", StringComparison.OrdinalIgnoreCase) == true ||
                         p.Name?.Contains("ToVersion", StringComparison.OrdinalIgnoreCase) == true ||
                         p.Name?.Contains("Version", StringComparison.OrdinalIgnoreCase) == true);

            if (!supportsVersionFiltering)
            {
                violations.Add(CreateViolation(
                    storeType,
                    $"Get method '{method.Name}' should support version range filtering",
                    "Add version filtering for efficient event retrieval"));
            }

            // Should return IAsyncEnumerable for streaming
            var returnsStream = method.ReturnType.Name.Contains("IAsyncEnumerable", StringComparison.OrdinalIgnoreCase) ||
                              method.ReturnType.Name.Contains("Stream", StringComparison.OrdinalIgnoreCase);

            if (!returnsStream && method.Name.Contains("Stream", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(CreateViolation(
                    storeType,
                    $"Stream method '{method.Name}' should return IAsyncEnumerable",
                    "Use streaming return types for better performance"));
            }
        }
    }

    private void ValidateEventRepositoryStructure(Type repoType, List<RuleViolation> violations)
    {
        // Event repositories should follow repository pattern
        var implementsRepositoryInterface = repoType.GetInterfaces()
            .Any(i => i.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase));

        if (!implementsRepositoryInterface)
        {
            violations.Add(CreateViolation(
                repoType,
                "Event repositories should implement repository interfaces",
                $"Make '{repoType.Name}' implement IRepository interface"));
        }

        // Should be in Infrastructure layer
        if (!IsInInfrastructureNamespace(repoType))
        {
            violations.Add(CreateViolation(
                repoType,
                "Event repositories should be in Infrastructure layer",
                $"Move '{repoType.Name}' to Infrastructure namespace"));
        }
    }

    private void ValidateEventRepositoryMethods(Type repoType, List<RuleViolation> violations)
    {
        var publicMethods = repoType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == repoType)
            .ToList();

        foreach (var method in publicMethods)
        {
            // Repository methods should be async
            if (!IsAsyncMethod(method))
            {
                violations.Add(CreateViolation(
                    repoType,
                    $"Repository method '{method.Name}' should be async",
                    "Make repository methods async"));
            }

            // Should follow repository naming conventions
            if (!IsRepositoryMethod(method.Name))
            {
                violations.Add(CreateViolation(
                    repoType,
                    $"Method '{method.Name}' doesn't follow repository naming conventions",
                    "Use Get, Add, Update, Delete, or Find prefixes"));
            }
        }
    }

    private void ValidateEventStorability(Type eventType, List<RuleViolation> violations)
    {
        // Events should be serializable
        var hasSerializableAttributes = eventType.GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("Serializable", StringComparison.OrdinalIgnoreCase) ||
                        attr.GetType().Name.Contains("JsonObject", StringComparison.OrdinalIgnoreCase) ||
                        attr.GetType().Name.Contains("DataContract", StringComparison.OrdinalIgnoreCase));

        var hasParameterlessConstructor = eventType.GetConstructors()
            .Any(c => c.GetParameters().Length == 0);

        if (!hasParameterlessConstructor && !hasSerializableAttributes)
        {
            violations.Add(CreateViolation(
                eventType,
                "Storable events should have parameterless constructor or serialization attributes",
                "Add parameterless constructor or serialization attributes"));
        }

        // Should not have complex types that can't be serialized
        var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            if (IsComplexNonSerializableType(property.PropertyType))
            {
                violations.Add(CreateViolation(
                    eventType,
                    $"Property '{property.Name}' uses type that may not serialize properly",
                    "Use simple types or ensure proper serialization"));
            }
        }
    }

    private void ValidateEventVersioning(Type eventType, List<RuleViolation> violations)
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
                "Storable events should have version information for schema evolution",
                "Add Version or SchemaVersion property"));
        }

        // Should have event type identifier for deserialization
        var hasEventTypeProperty = properties.Any(p => 
            p.Name.Equals("EventType", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("Type", StringComparison.OrdinalIgnoreCase));

        if (!hasEventTypeProperty)
        {
            violations.Add(CreateViolation(
                eventType,
                "Storable events should have event type identifier",
                "Add EventType property for proper deserialization"));
        }
    }

    private void ValidateEventMetadata(Type eventType, List<RuleViolation> violations)
    {
        var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Should have event ID
        var hasIdProperty = properties.Any(p => 
            p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals("EventId", StringComparison.OrdinalIgnoreCase));

        if (!hasIdProperty)
        {
            violations.Add(CreateViolation(
                eventType,
                "Storable events should have unique identifier",
                "Add Id or EventId property"));
        }

        // Should have timestamp
        var hasTimestampProperty = properties.Any(p => 
            p.Name.Contains("Timestamp", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("OccurredAt", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("CreatedAt", StringComparison.OrdinalIgnoreCase) ||
            p.PropertyType == typeof(DateTime) ||
            p.PropertyType == typeof(DateTimeOffset));

        if (!hasTimestampProperty)
        {
            violations.Add(CreateViolation(
                eventType,
                "Storable events should have timestamp information",
                "Add timestamp property for event ordering"));
        }

        // Should have aggregate ID for correlation
        var hasAggregateIdProperty = properties.Any(p => 
            p.Name.Contains("AggregateId", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("StreamId", StringComparison.OrdinalIgnoreCase));

        if (!hasAggregateIdProperty)
        {
            violations.Add(CreateViolation(
                eventType,
                "Storable events should have aggregate or stream identifier",
                "Add AggregateId or StreamId property"));
        }
    }

    private void ValidateAggregateEventStoreIntegration(Type aggregateType, List<RuleViolation> violations)
    {
        // Check if aggregate uses event store
        var constructors = aggregateType.GetConstructors();
        var usesEventStore = constructors.Any(c => 
            c.GetParameters().Any(p => IsEventStore(p.ParameterType)));

        var methods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var hasEventStoreUsage = methods.Any(m => 
            m.GetParameters().Any(p => IsEventStore(p.ParameterType)));

        // If aggregate has event-related functionality, it should integrate with event store
        var hasEventFunctionality = methods.Any(m => 
            m.Name.Contains("Event", StringComparison.OrdinalIgnoreCase)) ||
            aggregateType.GetProperties().Any(p => 
                p.Name.Contains("Event", StringComparison.OrdinalIgnoreCase));

        if (hasEventFunctionality && !usesEventStore && !hasEventStoreUsage)
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Aggregates with event functionality should integrate with event store",
                "Add event store dependency for event persistence"));
        }
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

    private static bool IsSaveMethod(string methodName)
    {
        return methodName.Contains("Save", StringComparison.OrdinalIgnoreCase) ||
               methodName.Contains("Append", StringComparison.OrdinalIgnoreCase) ||
               methodName.Contains("Store", StringComparison.OrdinalIgnoreCase) ||
               methodName.Contains("Persist", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGetMethod(string methodName)
    {
        return methodName.Contains("Get", StringComparison.OrdinalIgnoreCase) ||
               methodName.Contains("Load", StringComparison.OrdinalIgnoreCase) ||
               methodName.Contains("Read", StringComparison.OrdinalIgnoreCase) ||
               methodName.Contains("Retrieve", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRepositoryMethod(string methodName)
    {
        string[] repositoryPrefixes = { "Get", "Add", "Update", "Delete", "Find", "Save", "Load", "Create", "Remove" };
        return repositoryPrefixes.Any(prefix => methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsComplexNonSerializableType(Type type)
    {
        return !type.IsPrimitive && 
               type != typeof(string) && 
               type != typeof(DateTime) && 
               type != typeof(DateTimeOffset) && 
               type != typeof(Guid) && 
               !type.IsEnum &&
               type.Namespace?.StartsWith("System") != true;
    }
}