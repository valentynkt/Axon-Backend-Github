using Axon.ArchitectureTests.Framework.Contracts;
using Axon.ArchitectureTests.Framework.Rules;
using System.Reflection;

namespace Axon.ArchitectureTests.Core.Rules.EventDriven;

/// <summary>
/// Rule to validate event sourcing implementation patterns including projections and read-side consistency.
/// </summary>
public sealed class EventSourcingRule : ArchitectureRuleBase
{
    public override string RuleId => "EVT005";
    public override string Name => "Event Sourcing Rule";
    public override string Description => "Validates that event sourcing patterns are properly implemented including projections and consistency";
    public override string Category => "EventDriven";
    public override RuleSeverity Severity => RuleSeverity.Error;

    protected override async Task<IEnumerable<RuleViolation>> ExecuteValidationAsync(
        IArchitectureContext context, 
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        
        await Task.Run(() =>
        {
            var eventSourcedAggregateTypes = context.Types.Where(IsEventSourcedAggregate).ToList();
            var projectionTypes = context.Types.Where(IsProjection).ToList();
            var projectionHandlerTypes = context.Types.Where(IsProjectionHandler).ToList();
            var snapshotTypes = context.Types.Where(IsSnapshot).ToList();
            var readModelTypes = context.Types.Where(IsReadModel).ToList();

            // Validate event-sourced aggregates
            foreach (var aggregateType in eventSourcedAggregateTypes)
            {
                ValidateEventSourcedAggregateStructure(aggregateType, violations);
                ValidateEventSourcedAggregateRehydration(aggregateType, violations);
                ValidateEventSourcedAggregateEventApplication(aggregateType, violations);
                ValidateEventSourcedAggregateSnapshotSupport(aggregateType, violations);
            }

            // Validate projections
            foreach (var projectionType in projectionTypes)
            {
                ValidateProjectionStructure(projectionType, violations);
                ValidateProjectionEventHandling(projectionType, violations);
                ValidateProjectionIdempotency(projectionType, violations);
            }

            // Validate projection handlers
            foreach (var handlerType in projectionHandlerTypes)
            {
                ValidateProjectionHandlerStructure(handlerType, violations);
                ValidateProjectionHandlerEventualConsistency(handlerType, violations);
                ValidateProjectionHandlerErrorHandling(handlerType, violations);
            }

            // Validate snapshots
            foreach (var snapshotType in snapshotTypes)
            {
                ValidateSnapshotStructure(snapshotType, violations);
                ValidateSnapshotSerialization(snapshotType, violations);
            }

            // Validate read models
            foreach (var readModelType in readModelTypes)
            {
                ValidateReadModelStructure(readModelType, violations);
                ValidateReadModelConsistency(readModelType, violations);
            }

        }, cancellationToken);

        return violations;
    }

    private static bool IsEventSourcedAggregate(Type type)
    {
        return (type.Name.EndsWith("Aggregate", StringComparison.OrdinalIgnoreCase) ||
                InheritsFromAggregateRoot(type)) &&
               (type.GetMethods().Any(m => m.Name.Contains("ApplyEvent", StringComparison.OrdinalIgnoreCase) ||
                                          m.Name.Contains("Apply", StringComparison.OrdinalIgnoreCase)) ||
                type.GetMethods().Any(m => m.Name.Contains("LoadFromHistory", StringComparison.OrdinalIgnoreCase) ||
                                          m.Name.Contains("RehydrateFrom", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsProjection(Type type)
    {
        return type.Name.EndsWith("Projection", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("ReadModel", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("View", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("Projection", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProjectionHandler(Type type)
    {
        return type.Name.EndsWith("ProjectionHandler", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("ViewHandler", StringComparison.OrdinalIgnoreCase) ||
               (type.Name.Contains("Projection", StringComparison.OrdinalIgnoreCase) &&
                type.Name.Contains("Handler", StringComparison.OrdinalIgnoreCase)) ||
               type.GetMethods().Any(m => m.Name.Contains("Project", StringComparison.OrdinalIgnoreCase) &&
                                         m.GetParameters().Any(p => IsEvent(p.ParameterType)));
    }

    private static bool IsSnapshot(Type type)
    {
        return type.Name.EndsWith("Snapshot", StringComparison.OrdinalIgnoreCase) ||
               type.Name.Contains("Snapshot", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("Snapshot", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsReadModel(Type type)
    {
        return type.Name.EndsWith("ReadModel", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase) ||
               type.Name.EndsWith("QueryModel", StringComparison.OrdinalIgnoreCase) ||
               (IsInApplicationNamespace(type) && type.Name.Contains("Model", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEvent(Type type)
    {
        return type.Name.EndsWith("Event", StringComparison.OrdinalIgnoreCase) ||
               type.GetInterfaces().Any(i => i.Name.Contains("Event", StringComparison.OrdinalIgnoreCase));
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

    private void ValidateEventSourcedAggregateStructure(Type aggregateType, List<RuleViolation> violations)
    {
        // Should be in Domain layer
        if (!IsInDomainNamespace(aggregateType))
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Event-sourced aggregates should be in Domain layer",
                $"Move '{aggregateType.Name}' to Domain namespace"));
        }

        // Should have ApplyEvent methods
        var applyMethods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Apply", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!applyMethods.Any())
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Event-sourced aggregates should have ApplyEvent methods",
                "Add Apply methods for event handling"));
        }

        // Should have LoadFromHistory or similar method
        var loadMethods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("LoadFromHistory", StringComparison.OrdinalIgnoreCase) ||
                       m.Name.Contains("RehydrateFrom", StringComparison.OrdinalIgnoreCase) ||
                       m.Name.Contains("RestoreFrom", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!loadMethods.Any())
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Event-sourced aggregates should have LoadFromHistory method",
                "Add LoadFromHistory method for rehydration"));
        }

        // Should have version property for optimistic concurrency
        var properties = aggregateType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var hasVersionProperty = properties.Any(p => 
            p.Name.Equals("Version", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Version", StringComparison.OrdinalIgnoreCase));

        if (!hasVersionProperty)
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Event-sourced aggregates should have Version property",
                "Add Version property for optimistic concurrency control"));
        }
    }

    private void ValidateEventSourcedAggregateRehydration(Type aggregateType, List<RuleViolation> violations)
    {
        var loadMethods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("LoadFromHistory", StringComparison.OrdinalIgnoreCase) ||
                       m.Name.Contains("RehydrateFrom", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in loadMethods)
        {
            // Load methods should accept event collections
            var hasEventCollectionParameter = method.GetParameters()
                .Any(p => p.ParameterType.Name.Contains("IEnumerable", StringComparison.OrdinalIgnoreCase) ||
                         p.ParameterType.Name.Contains("List", StringComparison.OrdinalIgnoreCase) ||
                         p.ParameterType.Name.Contains("Event", StringComparison.OrdinalIgnoreCase));

            if (!hasEventCollectionParameter)
            {
                violations.Add(CreateViolation(
                    aggregateType,
                    $"Load method '{method.Name}' should accept event collection",
                    "Accept IEnumerable<Event> parameter"));
            }

            // Load methods should be static factory methods or void
            if (method.ReturnType != typeof(void) && method.ReturnType != aggregateType && !method.IsStatic)
            {
                violations.Add(CreateViolation(
                    aggregateType,
                    $"Load method '{method.Name}' should be static factory or void instance method",
                    "Make load method static factory or void instance method"));
            }
        }
    }

    private void ValidateEventSourcedAggregateEventApplication(Type aggregateType, List<RuleViolation> violations)
    {
        var applyMethods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Apply", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in applyMethods)
        {
            // Apply methods should be idempotent (void return)
            if (method.ReturnType != typeof(void))
            {
                violations.Add(CreateViolation(
                    aggregateType,
                    $"Apply method '{method.Name}' should be void for idempotency",
                    "Make Apply methods void"));
            }

            // Apply methods should have exactly one event parameter
            var eventParameters = method.GetParameters()
                .Where(p => IsEvent(p.ParameterType))
                .ToList();

            if (eventParameters.Count != 1)
            {
                violations.Add(CreateViolation(
                    aggregateType,
                    $"Apply method '{method.Name}' should have exactly one event parameter",
                    "Apply methods should handle one event type"));
            }

            // Apply methods should be private or protected
            if (method.IsPublic)
            {
                violations.Add(CreateViolation(
                    aggregateType,
                    $"Apply method '{method.Name}' should be private or protected",
                    "Make Apply methods non-public for encapsulation"));
            }
        }

        // Should have Apply method for each business method that produces events
        var businessMethods = aggregateType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == aggregateType)
            .ToList();

        var businessMethodsWithoutApply = businessMethods
            .Where(bm => !applyMethods.Any(am => 
                am.Name.Contains(bm.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (businessMethodsWithoutApply.Any())
        {
            foreach (var method in businessMethodsWithoutApply.Take(3)) // Limit to avoid too many violations
            {
                violations.Add(CreateViolation(
                    aggregateType,
                    $"Business method '{method.Name}' should have corresponding Apply method",
                    "Add Apply method for event-driven state changes"));
            }
        }
    }

    private void ValidateEventSourcedAggregateSnapshotSupport(Type aggregateType, List<RuleViolation> violations)
    {
        // Check if aggregate supports snapshots (optional but recommended for performance)
        var hasSnapshotMethods = aggregateType.GetMethods()
            .Any(m => m.Name.Contains("Snapshot", StringComparison.OrdinalIgnoreCase) ||
                     m.Name.Contains("CreateSnapshot", StringComparison.OrdinalIgnoreCase) ||
                     m.Name.Contains("RestoreFromSnapshot", StringComparison.OrdinalIgnoreCase));

        var properties = aggregateType.GetProperties();
        var hasLargeState = properties.Length > 10; // Heuristic for complex aggregates

        if (hasLargeState && !hasSnapshotMethods)
        {
            violations.Add(CreateViolation(
                aggregateType,
                "Complex aggregates should support snapshots for performance",
                "Add snapshot creation and restoration methods"));
        }
    }

    private void ValidateProjectionStructure(Type projectionType, List<RuleViolation> violations)
    {
        // Projections should be in Application or Infrastructure layer
        if (!IsInApplicationNamespace(projectionType) && !IsInInfrastructureNamespace(projectionType))
        {
            violations.Add(CreateViolation(
                projectionType,
                "Projections should be in Application or Infrastructure layer",
                $"Move '{projectionType.Name}' to appropriate layer"));
        }

        // Projections should be immutable or have controlled mutability
        var publicSetters = projectionType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.SetMethod?.IsPublic == true)
            .ToList();

        if (publicSetters.Any())
        {
            violations.Add(CreateViolation(
                projectionType,
                "Projections should be immutable or have controlled mutability",
                "Use init-only setters or private setters"));
        }

        // Should have identifier for correlation
        var properties = projectionType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var hasIdProperty = properties.Any(p => 
            p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Id", StringComparison.OrdinalIgnoreCase));

        if (!hasIdProperty)
        {
            violations.Add(CreateViolation(
                projectionType,
                "Projections should have identifier property",
                "Add Id property for correlation"));
        }
    }

    private void ValidateProjectionEventHandling(Type projectionType, List<RuleViolation> violations)
    {
        var methods = projectionType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        
        // Should have event handling methods
        var eventHandlingMethods = methods.Where(m => 
            m.Name.Contains("Handle", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Apply", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Project", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!eventHandlingMethods.Any())
        {
            violations.Add(CreateViolation(
                projectionType,
                "Projections should have event handling methods",
                "Add Handle, Apply, or Project methods"));
        }

        foreach (var method in eventHandlingMethods)
        {
            // Event handling methods should be void or return Task
            if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
            {
                violations.Add(CreateViolation(
                    projectionType,
                    $"Event handling method '{method.Name}' should be void or return Task",
                    "Use void or Task return type"));
            }

            // Should have event parameter
            var hasEventParameter = method.GetParameters()
                .Any(p => IsEvent(p.ParameterType));

            if (!hasEventParameter)
            {
                violations.Add(CreateViolation(
                    projectionType,
                    $"Event handling method '{method.Name}' should have event parameter",
                    "Add event parameter to handling method"));
            }
        }
    }

    private void ValidateProjectionIdempotency(Type projectionType, List<RuleViolation> violations)
    {
        var eventHandlingMethods = projectionType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Handle", StringComparison.OrdinalIgnoreCase) ||
                       m.Name.Contains("Apply", StringComparison.OrdinalIgnoreCase) ||
                       m.Name.Contains("Project", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in eventHandlingMethods)
        {
            // Projections should handle duplicate events gracefully
            var parameters = method.GetParameters();
            var eventParams = parameters.Where(p => IsEvent(p.ParameterType)).ToList();

            foreach (var eventParam in eventParams)
            {
                var eventType = eventParam.ParameterType;
                var hasIdProperty = eventType.GetProperties()
                    .Any(p => p.Name.Contains("Id", StringComparison.OrdinalIgnoreCase));

                if (!hasIdProperty)
                {
                    violations.Add(CreateViolation(
                        projectionType,
                        "Events should have ID for projection idempotency",
                        "Ensure events have unique identifiers"));
                }
            }

            // Check for idempotency implementation patterns
            var hasIdempotencyCheck = projectionType.GetProperties().Any(p => 
                p.Name.Contains("LastProcessedEventId", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Version", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Timestamp", StringComparison.OrdinalIgnoreCase));

            if (!hasIdempotencyCheck)
            {
                violations.Add(CreateViolation(
                    projectionType,
                    "Projections should implement idempotency checks",
                    "Add LastProcessedEventId or Version tracking"));
            }
        }
    }

    private void ValidateProjectionHandlerStructure(Type handlerType, List<RuleViolation> violations)
    {
        // Should be in Application or Infrastructure layer
        if (!IsInApplicationNamespace(handlerType) && !IsInInfrastructureNamespace(handlerType))
        {
            violations.Add(CreateViolation(
                handlerType,
                "Projection handlers should be in Application or Infrastructure layer",
                $"Move '{handlerType.Name}' to appropriate layer"));
        }

        // Should have Handle methods
        var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!handleMethods.Any())
        {
            violations.Add(CreateViolation(
                handlerType,
                "Projection handlers should have Handle methods",
                "Add Handle methods for event processing"));
        }

        foreach (var method in handleMethods)
        {
            // Handle methods should be async
            if (!IsAsyncMethod(method))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Handle method should be async for projection updates",
                    "Make Handle method async"));
            }
        }
    }

    private void ValidateProjectionHandlerEventualConsistency(Type handlerType, List<RuleViolation> violations)
    {
        // Projection handlers should handle eventual consistency
        var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in handleMethods)
        {
            // Should not require immediate consistency (no synchronous database operations)
            var parameters = method.GetParameters();
            var hasSyncDbContext = parameters.Any(p => 
                p.ParameterType.Name.Contains("DbContext", StringComparison.OrdinalIgnoreCase) &&
                !IsAsyncMethod(method));

            if (hasSyncDbContext)
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Projection handler should use async patterns for eventual consistency",
                    "Use async database operations"));
            }

            // Should handle ordering issues
            var handlesOrdering = method.GetParameters().Any(p => 
                p.Name?.Contains("Version", StringComparison.OrdinalIgnoreCase) == true ||
                p.Name?.Contains("Sequence", StringComparison.OrdinalIgnoreCase) == true ||
                IsEvent(p.ParameterType) && p.ParameterType.GetProperties().Any(prop => 
                    prop.Name.Contains("Version", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("Timestamp", StringComparison.OrdinalIgnoreCase)));

            if (!handlesOrdering)
            {
                violations.Add(CreateViolation(
                    handlerType,
                    "Projection handlers should handle event ordering",
                    "Consider event versioning or timestamps"));
            }
        }
    }

    private void ValidateProjectionHandlerErrorHandling(Type handlerType, List<RuleViolation> violations)
    {
        var handleMethods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var method in handleMethods)
        {
            // Should return Result type for error handling
            if (!ReturnsResultType(method) && method.ReturnType != typeof(Task))
            {
                violations.Add(CreateViolation(
                    handlerType,
                    $"Projection handler should return Result type for error handling",
                    "Return Result type for proper error handling"));
            }

            // Should have retry mechanism attributes
            var hasRetryAttribute = method.GetCustomAttributes()
                .Any(attr => attr.GetType().Name.Contains("Retry", StringComparison.OrdinalIgnoreCase));

            if (!hasRetryAttribute)
            {
                violations.Add(CreateViolation(
                    handlerType,
                    "Projection handlers should have retry mechanisms",
                    "Add retry policies for resilient projection updates"));
            }
        }
    }

    private void ValidateSnapshotStructure(Type snapshotType, List<RuleViolation> violations)
    {
        // Snapshots should be immutable
        var mutableProperties = snapshotType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.SetMethod?.IsPublic == true)
            .ToList();

        if (mutableProperties.Any())
        {
            violations.Add(CreateViolation(
                snapshotType,
                "Snapshots should be immutable",
                "Use init-only setters or readonly properties"));
        }

        // Should have version information
        var properties = snapshotType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var hasVersionProperty = properties.Any(p => 
            p.Name.Equals("Version", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Version", StringComparison.OrdinalIgnoreCase));

        if (!hasVersionProperty)
        {
            violations.Add(CreateViolation(
                snapshotType,
                "Snapshots should have version information",
                "Add Version property to snapshot"));
        }

        // Should have aggregate identifier
        var hasAggregateId = properties.Any(p => 
            p.Name.Contains("AggregateId", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Id", StringComparison.OrdinalIgnoreCase));

        if (!hasAggregateId)
        {
            violations.Add(CreateViolation(
                snapshotType,
                "Snapshots should have aggregate identifier",
                "Add AggregateId property"));
        }
    }

    private void ValidateSnapshotSerialization(Type snapshotType, List<RuleViolation> violations)
    {
        // Snapshots should be serializable
        var hasSerializationSupport = snapshotType.GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("Serializable", StringComparison.OrdinalIgnoreCase) ||
                        attr.GetType().Name.Contains("JsonObject", StringComparison.OrdinalIgnoreCase));

        var hasParameterlessConstructor = snapshotType.GetConstructors()
            .Any(c => c.GetParameters().Length == 0);

        if (!hasParameterlessConstructor && !hasSerializationSupport)
        {
            violations.Add(CreateViolation(
                snapshotType,
                "Snapshots should be serializable",
                "Add parameterless constructor or serialization attributes"));
        }

        // Should not contain complex non-serializable types
        var properties = snapshotType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            if (IsComplexNonSerializableType(property.PropertyType))
            {
                violations.Add(CreateViolation(
                    snapshotType,
                    $"Snapshot property '{property.Name}' may not serialize properly",
                    "Use simple serializable types in snapshots"));
            }
        }
    }

    private void ValidateReadModelStructure(Type readModelType, List<RuleViolation> violations)
    {
        // Read models should be optimized for queries
        var properties = readModelType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Should have meaningful query-focused properties
        if (properties.Length < 2)
        {
            violations.Add(CreateViolation(
                readModelType,
                "Read models should have meaningful properties for queries",
                "Add query-focused properties to read model"));
        }

        // Should not have business logic methods
        var businessMethods = readModelType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == readModelType)
            .Where(m => !IsQueryMethod(m.Name))
            .ToList();

        if (businessMethods.Any())
        {
            violations.Add(CreateViolation(
                readModelType,
                "Read models should not contain business logic",
                "Move business logic to domain services"));
        }
    }

    private void ValidateReadModelConsistency(Type readModelType, List<RuleViolation> violations)
    {
        var properties = readModelType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Should have timestamp for consistency tracking
        var hasTimestamp = properties.Any(p => 
            p.Name.Contains("Timestamp", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("LastUpdated", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("UpdatedAt", StringComparison.OrdinalIgnoreCase) ||
            p.PropertyType == typeof(DateTime) ||
            p.PropertyType == typeof(DateTimeOffset));

        if (!hasTimestamp)
        {
            violations.Add(CreateViolation(
                readModelType,
                "Read models should have timestamp for consistency tracking",
                "Add timestamp property to track updates"));
        }

        // Should have version or sequence number for ordering
        var hasVersioning = properties.Any(p => 
            p.Name.Contains("Version", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Sequence", StringComparison.OrdinalIgnoreCase));

        if (!hasVersioning)
        {
            violations.Add(CreateViolation(
                readModelType,
                "Read models should have versioning for eventual consistency",
                "Add Version or Sequence property"));
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

    private static bool IsQueryMethod(string methodName)
    {
        string[] queryPrefixes = { "Get", "Find", "Search", "Query", "List", "Count", "Exists", "Is" };
        return queryPrefixes.Any(prefix => methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
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