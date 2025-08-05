# Repository Caching Configuration Examples

This document shows how to configure the new CQRS-compliant repository caching decorators.

## Basic Setup in Program.cs

```csharp
using BuildingBlocks.Persistence.Caching;

var builder = WebApplication.CreateBuilder(args);

// Register your base repositories first
builder.Services.AddScoped<IReadRepository<User, long>, UserReadRepository>();
builder.Services.AddScoped<IWriteRepository<User, long>, UserWriteRepository>();
builder.Services.AddScoped<IReadRepository<Order, long>, OrderReadRepository>();
builder.Services.AddScoped<IWriteRepository<Order, long>, OrderWriteRepository>();

// Add caching decorators to ALL repositories (after base registrations)
builder.Services.AddRepositoryCaching(cacheExpiration: TimeSpan.FromMinutes(30));

var app = builder.Build();
```

## Fine-Grained Control for Specific Entities

```csharp
using BuildingBlocks.Persistence.Caching;

var builder = WebApplication.CreateBuilder(args);

// Register your base repositories
builder.Services.AddScoped<IReadRepository<User, long>, UserReadRepository>();
builder.Services.AddScoped<IWriteRepository<User, long>, UserWriteRepository>();
builder.Services.AddScoped<IReadRepository<Order, long>, OrderReadRepository>();
builder.Services.AddScoped<IWriteRepository<Order, long>, OrderWriteRepository>();

// Add caching only for User repositories (more control)
builder.Services.AddRepositoryCachingFor<User, long>(cacheExpiration: TimeSpan.FromHours(1));

// Orders don't get caching (maybe they change too frequently)

var app = builder.Build();
```

## Module-Based Configuration

```csharp
// In your module's InfrastructureServiceRegistration.cs
public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register EF Core context
        services.AddDbContext<ChatDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Register base repositories
        services.AddScoped<IReadRepository<Conversation, long>, ConversationReadRepository>();
        services.AddScoped<IWriteRepository<Conversation, long>, ConversationWriteRepository>();
        services.AddScoped<IReadRepository<Message, long>, MessageReadRepository>();
        services.AddScoped<IWriteRepository<Message, long>, MessageWriteRepository>();

        // Add caching decorators (this should be LAST)
        services.AddRepositoryCaching(cacheExpiration: TimeSpan.FromMinutes(15));

        return services;
    }
}
```

## Usage in Application Layer

```csharp
// Query Handler (uses IReadRepository with automatic caching)
public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly IReadRepository<User, long> _userRepository;

    public GetUserByIdQueryHandler(IReadRepository<User, long> userRepository)
    {
        _userRepository = userRepository; // This is automatically decorated with caching
    }

    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        // This call will check cache first, then hit database if needed
        var user = await _userRepository.FindByIdAsync(request.UserId, cancellationToken);
        return user?.ToDto();
    }
}

// Command Handler (uses IWriteRepository with automatic cache invalidation)
public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand>
{
    private readonly IWriteRepository<User, long> _userRepository;

    public UpdateUserCommandHandler(IWriteRepository<User, long> userRepository)
    {
        _userRepository = userRepository; // This is automatically decorated with cache invalidation
    }

    public async Task Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user != null)
        {
            user.UpdateEmail(request.Email);
            // This call will automatically invalidate cache entries
            await _userRepository.UpdateAsync(user, cancellationToken);
        }
    }
}
```

## Key Benefits

1. **CQRS Compliance**: Read and write operations are completely separated
2. **Zero Code Changes**: Existing query/command handlers work unchanged
3. **Automatic Cache Management**: Cache invalidation happens automatically on writes
4. **Flexible Configuration**: Enable caching per entity type or globally
5. **SOLID Principles**: Each decorator has a single responsibility
6. **Decorator Pattern**: Clean separation of caching concerns from repository logic

## Important Notes

- Call `AddRepositoryCaching()` AFTER registering your base repositories
- Cache is automatically invalidated on any write operation (Add, Update, Delete)
- Complex queries with predicates are not cached to avoid key complexity
- Use `TimeSpan.FromMinutes(15)` as a reasonable default for cache expiration
- The decorators are registered as the same interface, so dependency injection transparently provides the cached version