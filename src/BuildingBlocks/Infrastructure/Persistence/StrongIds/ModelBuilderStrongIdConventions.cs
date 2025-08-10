using System.Reflection;
using BuildingBlocks.Core.Domain.Primitives;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence.StrongIds;

/// <summary>
/// Extension methods to automatically configure StrongId value converters and comparers.
/// Scans the model for properties implementing IStrongId&lt;T&gt; and applies conventions.
/// </summary>
public static class ModelBuilderStrongIdConventions
{
    /// <summary>
    /// Applies StrongId conventions to all properties in the model.
    /// Automatically registers value converters and comparers for StrongId properties.
    /// </summary>
    public static void ApplyStrongIdConventions(this ModelBuilder modelBuilder)
    {
        var strongIdProperties = new List<(Type EntityType, string PropertyName, Type StrongIdType, Type PrimitiveType)>();

        // Scan all entity types for StrongId properties
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            
            foreach (var property in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propertyType = property.PropertyType;
                
                // Handle nullable StrongId properties (TStrongId?)
                var actualType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
                
                if (TryGetStrongIdPrimitive(actualType, out var primitiveType))
                {
                    strongIdProperties.Add((clrType, property.Name, actualType, primitiveType));
                }
            }
        }

        // Apply converters and comparers for each StrongId property
        foreach (var (entityType, propertyName, strongIdType, primitiveType) in strongIdProperties)
        {
            ApplyStrongIdConversion(modelBuilder, entityType, propertyName, strongIdType, primitiveType);
        }
    }

    private static bool TryGetStrongIdPrimitive(Type candidate, out Type primitiveType)
    {
        // Walk inheritance chain to find StrongId<TPrimitive>
        var current = candidate;
        while (current is not null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(StrongId<>))
            {
                primitiveType = current.GetGenericArguments()[0];
                return true;
            }
            current = current.BaseType;
        }

        primitiveType = null!;
        return false;
    }

    private static void ApplyStrongIdConversion(
        ModelBuilder modelBuilder,
        Type entityType,
        string propertyName,
        Type strongIdType,
        Type primitiveType)
    {
        try
        {
            // Create converter and comparer instances using reflection
            var converterType = typeof(StrongIdValueConverter<,>).MakeGenericType(strongIdType, primitiveType);
            var comparerType = typeof(StrongIdValueComparer<,>).MakeGenericType(strongIdType, primitiveType);
            
            var converter = Activator.CreateInstance(converterType);
            var comparer = Activator.CreateInstance(comparerType);

            // Apply to the property
            modelBuilder.Entity(entityType)
                .Property(propertyName)
                .HasConversion(converter as Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter)
                .Metadata.SetValueComparer(comparer as Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to configure StrongId conversion for {entityType.Name}.{propertyName} of type {strongIdType.Name}: {ex.Message}",
                ex);
        }
    }
}