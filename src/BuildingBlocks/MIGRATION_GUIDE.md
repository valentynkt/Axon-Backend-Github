# 🚀 Persistence Architecture Migration Guide

## Overview

This guide helps you migrate from the old inconsistent persistence architecture to the new unified CQRS-based persistence system.

## ⚠️ What Changed

### **DEPRECATED (Remove these):**
- `BuildingBlocks.EFCore.IDbContext` → Use `BuildingBlocks.Persistence.IDbContext`
- `BuildingBlocks.Postgres.IPostgresDbContext` → Use `BuildingBlocks.Persistence.IWriteDbContext/IReadDbContext`
- `BuildingBlocks.Postgres.IRepository<T, TId>` → Use `BuildingBlocks.Persistence.IReadRepository<T, TId>` + `IWriteRepository<T, TId>`
- `BuildingBlocks.Postgres.IUnitOfWork` → Use `BuildingBlocks.Persistence.IWriteUnitOfWork`
- `BuildingBlocks.Postgres.PostgresRepository<T, TId>` → Use `PostgresReadRepository<T, TId>` + `PostgresWriteRepository<T, TId>`

### **NEW UNIFIED ARCHITECTURE:**
```
BuildingBlocks/
├── Persistence/           # 🆕 Unified CQRS Architecture
│   ├── Common/           # Shared interfaces
│   │   ├── IDbContext.cs
│   │   ├── IWriteDbContext.cs
│   │   ├── IReadDbContext.cs
│   │   ├── IWriteRepository.cs
│   │   ├── IReadRepository.cs
│   │   └── IWriteUnitOfWork.cs
│   ├── Write/            # Command side
│   │   ├── WriteDbContextBase.cs
│   │   ├── PostgresWriteRepository.cs
│   │   └── PostgresWriteUnitOfWork.cs
│   ├── Read/             # Query side
│   │   ├── ReadDbContextBase.cs
│   │   └── PostgresReadRepository.cs
│   └── Extensions.cs     # Unified registration
├── EFCore/              # ❌ DEPRECATED
└── Postgres/            # ❌ DEPRECATED
```

## 🔄 Migration Steps

### **Step 1: Update Module DbContexts**

**OLD WAY:**
```csharp
public class IdentityContext : IdentityDbContext, IDbContext  // ❌ DEPRECATED
{
    // Mixed read/write operations
}
```

**NEW WAY:**
```csharp
// Separate contexts for CQRS
public class IdentityWriteContext : WriteDbContextBase<IdentityWriteContext>, IWriteDbContext<IdentityWriteContext>
{
    public override string ModuleName => "Identity";
    // Write operations only
}

public class IdentityReadContext : ReadDbContextBase<IdentityReadContext>, IReadDbContext<IdentityReadContext>
{
    public override string ModuleName => "Identity";
    // Read operations only (no tracking)
}
```

### **Step 2: Update Domain Models**

**OLD WAY:**
```csharp
public class User : IdentityUser<Guid>, IVersion  // ❌ No domain events
{
    public string FirstName { get; set; }
}
```

**NEW WAY:**
```csharp
public class User : IdentityUser<Guid>, IAggregate<Guid>, IVersion  // ✅ Domain events
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    public string FirstName { get; init; }
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    public IEvent[] ClearDomainEvents()
    {
        var events = _domainEvents.ToArray();
        _domainEvents.Clear();
        return events;
    }
    
    public static User Create(string firstName, string email)
    {
        var user = new User { FirstName = firstName, Email = email };
        user._domainEvents.Add(new UserRegisteredDomainEvent(user.Id, firstName, email));
        return user;
    }
}
```

### **Step 3: Update Repositories**

**OLD WAY:**
```csharp
public interface IUserRepository : IRepository<User, Guid>  // ❌ Mixed read/write
{
}

public class UserRepository : PostgresRepository<User, Guid>, IUserRepository  // ❌ DEPRECATED
{
}
```

**NEW WAY:**
```csharp
// Separate read and write repositories
public interface IUserWriteRepository : IWriteRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}

public interface IUserReadRepository : IReadRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetUsersByRoleAsync(string roleName, CancellationToken cancellationToken = default);
}

public class UserWriteRepository : PostgresWriteRepository<User, Guid>, IUserWriteRepository
{
    public UserWriteRepository(IdentityWriteContext context) : base(context) { }
    
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }
}

public class UserReadRepository : PostgresReadRepository<User, Guid>, IUserReadRepository
{
    public UserReadRepository(IdentityReadContext context) : base(context) { }
    
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await GetQueryable().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }
}
```

### **Step 4: Update Command Handlers**

**OLD WAY:**
```csharp
public class RegisterUserHandler : ICommandHandler<RegisterUser, RegisterUserResult>
{
    private readonly UserManager<User> _userManager;  // ❌ Direct EF dependency
    
    public async Task<RegisterUserResult> Handle(RegisterUser request, CancellationToken cancellationToken)
    {
        var user = new User { FirstName = request.FirstName, Email = request.Email };
        await _userManager.CreateAsync(user, request.Password);  // ❌ No domain events
        return new RegisterUserResult(user.Id);
    }
}
```

**NEW WAY:**
```csharp
public class RegisterUserHandler : ICommandHandler<RegisterUser, RegisterUserResult>
{
    private readonly IUserWriteRepository _userWriteRepository;
    private readonly IWriteUnitOfWork<IdentityWriteContext> _unitOfWork;  // ✅ CQRS UoW
    
    public async Task<RegisterUserResult> Handle(RegisterUser request, CancellationToken cancellationToken)
    {
        // Create aggregate using factory method (triggers domain events)
        var user = User.Create(request.FirstName, request.Email);
        
        await _userWriteRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);  // ✅ Publishes domain events
        
        return new RegisterUserResult(user.Id);
    }
}
```

### **Step 5: Update Query Handlers**

**OLD WAY:**
```csharp
public class GetUserQueryHandler : IQueryHandler<GetUserQuery, UserDto>
{
    private readonly IRepository<User, Guid> _repository;  // ❌ Mixed repository
    
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _repository.FindByIdAsync(request.UserId, cancellationToken);  // ❌ Tracking enabled
        return user.ToDto();
    }
}
```

**NEW WAY:**
```csharp
public class GetUserQueryHandler : IQueryHandler<GetUserQuery, UserDto>
{
    private readonly IUserReadRepository _userReadRepository;  // ✅ Read-only
    
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userReadRepository.FindByIdAsync(request.UserId, cancellationToken);  // ✅ No tracking
        return user.ToDto();
    }
}
```

### **Step 6: Update DI Registration**

**OLD WAY:**
```csharp
public static WebApplicationBuilder AddIdentityModules(this WebApplicationBuilder builder)
{
    builder.AddCustomDbContext<IdentityContext>(nameof(Identity));  // ❌ Single context
    builder.Services.AddScoped<IUserRepository, UserRepository>();  // ❌ Mixed repository
    return builder;
}
```

**NEW WAY:**
```csharp
public static WebApplicationBuilder AddIdentityModules(this WebApplicationBuilder builder)
{
    // ✅ Separate contexts for CQRS
    builder.Services.AddCqrsPersistence<IdentityWriteContext, IdentityReadContext>(
        builder.Configuration, "IdentityConnection");
    
    // ✅ Separate repositories
    builder.Services.AddScoped<IUserWriteRepository, UserWriteRepository>();
    builder.Services.AddScoped<IUserReadRepository, UserReadRepository>();
    
    return builder;
}
```

## 🎯 Key Benefits

### **✅ BEFORE vs AFTER:**

| Aspect | OLD (Inconsistent) | NEW (Unified CQRS) |
|--------|-------------------|---------------------|
| **Architecture** | Mixed read/write | Clean CQRS separation |
| **Interfaces** | 3 competing DbContext interfaces | 1 unified hierarchy |
| **Repositories** | Mixed CRUD operations | Separate read/write |
| **Performance** | Always tracking enabled | No-tracking for reads |
| **Domain Events** | Manual/missing | Automatic via aggregates |
| **Testability** | Tightly coupled | Clean abstractions |
| **Consistency** | Different patterns per module | Unified across all modules |

### **✅ What You Get:**

1. **🚀 Performance**: No-tracking queries, optimized read models
2. **🧩 Consistency**: Same patterns across all modules
3. **🔒 Type Safety**: Aggregate constraints for write repositories
4. **⚡ Domain Events**: Automatic event publishing via Unit of Work
5. **🧪 Testability**: Clean interfaces for mocking
6. **📈 Scalability**: Separate read/write optimization paths

## 🚨 Migration Checklist

- [ ] Update all DbContexts to inherit from `WriteDbContextBase` / `ReadDbContextBase`
- [ ] Implement `IAggregate<TId>` on all domain entities
- [ ] Create separate read/write repositories  
- [ ] Update command handlers to use `IWriteRepository` + `IWriteUnitOfWork`
- [ ] Update query handlers to use `IReadRepository`
- [ ] Replace old DI registrations with `AddCqrsPersistence`
- [ ] Remove deprecated interface usages
- [ ] Add domain events to aggregate factories
- [ ] Test write operations publish domain events
- [ ] Test read operations use no-tracking contexts

## 🔗 Integration Examples

See the Identity module (`src/Modules/Identity/src/`) for a complete working example of the new architecture.