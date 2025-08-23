# Mapster Usage Guide

## Overview

We've replaced our custom mapper infrastructure with **Mapster**, a high-performance object-to-object mapper for .NET. This significantly simplifies our codebase while providing better performance and more features.

## What Changed

### Before (400+ lines of custom infrastructure)
- Custom `IRequestMapper<,>` and `IResponseMapper<,>` interfaces
- Complex auto-registration with reflection
- `MapperFactory` with caching
- `BaseMapper` base class
- Individual mapper classes for each mapping

### After (Simple and Clean)
- Direct use of Mapster's `.Adapt<T>()` extension method
- Centralized configuration in `MapsterConfig.cs`
- No need for individual mapper classes
- Built-in DI support with `services.AddMapster()`

## Basic Usage

### Simple Mapping

```csharp
// Map request DTO to domain command
var command = requestDto.Adapt<ProcessMessageRequest>();

// Map domain response to API DTO
var responseDto = domainResult.Adapt<ChatTurnResponseDto>();
```

### Mapping with Result Pattern

Use our extension methods for Result pattern integration:

```csharp
// Safe mapping with error handling
var result = source.AdaptToResult<DestinationType>();
if (result.IsFailure)
    return Result.Failure<TResponse, Error>(result.Error);

// With null validation
var result = source.AdaptWithValidation<DestinationType>();
```

## Configuration

### Global Configuration

Global settings are configured in `MapsterConfig.cs`:

```csharp
public static void Configure()
{
    // Global settings
    TypeAdapterConfig.GlobalSettings.Default
        .NameMatchingStrategy(NameMatchingStrategy.Flexible)
        .PreserveReference(true);
}
```

### Type-Specific Mappings

Configure specific type mappings for complex scenarios:

```csharp
// Map StrongIds (extract the value)
TypeAdapterConfig<ProcessMessageResponse, ChatTurnResponseDto>
    .NewConfig()
    .Map(dest => dest.ConversationId, src => src.ConversationId.Value)
    .Map(dest => dest.UserMessageId, src => src.UserMessageId.Value)
    .Map(dest => dest.AssistantMessageId, src => src.AssistantMessageId.Value);

// Map Value Objects
TypeAdapterConfig<Source, Dest>
    .NewConfig()
    .Map(dest => dest.Message, src => src.MessageContent.ToString());
```

## Common Scenarios

### 1. Mapping in Endpoints

```csharp
public sealed class ChatTurnEndpoint : BaseResultEndpoint<ChatTurnRequestDto, ChatTurnResponseDto>
{
    protected override async Task<Result<ChatTurnResponseDto, Error>> ExecuteAsync(
        ChatTurnRequestDto request,
        CancellationToken ct)
    {
        // Map request to command
        var command = request.Adapt<ProcessMessageRequest>();

        // Execute business logic
        var result = await _dispatcher.ProcessMessageAsync(command, ct);
        if (result.IsFailure)
            return Result.Failure<ChatTurnResponseDto, Error>(result.Error);

        // Map result to response
        var response = result.Value.Adapt<ChatTurnResponseDto>();
        return Result.Success<ChatTurnResponseDto, Error>(response);
    }
}
```

### 2. Mapping Collections

```csharp
// Map list of items
var dtos = domainEntities.Adapt<List<ItemDto>>();

// Map with projection (for EF Core queries)
var dtos = await dbContext.Items
    .ProjectToType<ItemDto>()
    .ToListAsync();
```

### 3. Custom Conversion Logic

When you need custom mapping logic:

```csharp
TypeAdapterConfig<Order, OrderDto>
    .NewConfig()
    .MapWith(src => new OrderDto
    {
        Id = src.Id.Value,
        Total = src.Items.Sum(i => i.Price),
        Status = src.Status.ToString()
    });
```

### 4. Two-Way Mapping

Configure bidirectional mappings:

```csharp
TypeAdapterConfig<Entity, Dto>
    .NewConfig()
    .TwoWays();  // Automatically creates Dto -> Entity mapping
```

## Dependency Injection

Mapster is registered in `ServiceRegistration.cs`:

```csharp
// Configure Mapster
MapsterConfig.Configure();
services.AddMapster();
```

You can also inject `IMapper` if needed:

```csharp
public class MyService
{
    private readonly IMapper _mapper;
    
    public MyService(IMapper mapper)
    {
        _mapper = mapper;
    }
    
    public TDest Map<TDest>(object source)
    {
        return _mapper.Map<TDest>(source);
    }
}
```

## Best Practices

1. **Use `.Adapt<T>()` directly** - It's simple and readable
2. **Configure complex mappings centrally** - Keep all configurations in `MapsterConfig.cs`
3. **Leverage projection for queries** - Use `ProjectToType<T>()` with EF Core for optimal SQL
4. **Handle StrongIds explicitly** - Always map `.Value` for StrongId types
5. **Use Result pattern extensions** - For safe mapping with error handling

## Advanced Features

### Mapping with Runtime Parameters

```csharp
var dto = source.BuildAdapter()
    .AddParameters("userId", currentUserId)
    .AdaptToType<DestDto>();
```

### Conditional Mapping

```csharp
TypeAdapterConfig<Source, Dest>
    .NewConfig()
    .Map(dest => dest.Status,
         src => src.IsActive ? "Active" : "Inactive");
```

### After Mapping Actions

```csharp
TypeAdapterConfig<Source, Dest>
    .NewConfig()
    .AfterMapping((src, dest) =>
    {
        dest.ProcessedAt = DateTimeOffset.UtcNow;
    });
```

## Migration from Old System

If you're updating existing code:

1. **Remove mapper injections** - No need for `IRequestMapper` or `IResponseMapper`
2. **Delete mapper classes** - Remove individual mapper implementations
3. **Use `.Adapt<T>()`** - Replace `mapper.MapAsync()` with `.Adapt<T>()`
4. **Update DI registration** - Remove old mapper registrations

## Performance Benefits

Mapster provides:
- **Faster mapping** - Uses compiled expressions instead of reflection
- **Lower memory usage** - No intermediate objects or caching needed
- **AOT compatible** - Works with Native AOT compilation
- **Better debugging** - Can step into generated mapping code

## Troubleshooting

### Missing Mappings
If a mapping fails, check:
1. Property names match (or configure custom mapping)
2. Types are compatible
3. StrongIds are properly mapped to their values

### Circular References
Use `.PreserveReference(true)` in global config to handle circular references.

### Complex Nested Objects
Configure nested mappings separately or use `.Fork()` for isolated configurations.

## Summary

The migration to Mapster has:
- **Removed 400+ lines** of custom infrastructure
- **Simplified** our mapping code to just `.Adapt<T>()` calls
- **Improved performance** with compiled mappings
- **Added features** like projection support for EF Core
- **Reduced complexity** while maintaining all functionality

For more information, see the [official Mapster documentation](https://github.com/MapsterMapper/Mapster/wiki).