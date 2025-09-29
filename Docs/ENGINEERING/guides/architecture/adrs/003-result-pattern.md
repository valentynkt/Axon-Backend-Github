# ADR-003: Result Pattern for Error Handling

**Status**: ✅ Accepted
**Date**: 2025-01-16
**Deciders**: Engineering Team
**Technical Story**: Error Handling Strategy

---

## Context and Problem Statement

Axon needs a consistent error handling strategy that makes business logic errors explicit in the type system, avoids exceptions for control flow, and integrates well with ASP.NET Core HTTP responses.

---

## Decision Outcome

**Chosen**: Result<T, Error> pattern using CSharpFunctionalExtensions library.

**Rationale:**
- Errors visible in method signatures (compiler-enforced)
- No exceptions for business logic (exceptions only for programming errors)
- Railway-oriented programming enables clean error composition
- Integrates with ASP.NET Core via HttpResults extension

---

## Implementation

```csharp
// Domain method returns Result
public Result<WalletOwnership, Error> LinkWallet(Address address, ChainId chainId)
{
    if (_wallets.Any(w => w.Address == address))
        return Result.Failure<WalletOwnership, Error>(
            Error.BusinessRule("Wallet already linked"));

    var ownership = new WalletOwnership(Id, address, chainId);
    _wallets.Add(ownership);
    return Result.Success<WalletOwnership, Error>(ownership);
}

// Handler chains results
public async Task<Result<UserId, Error>> Handle(CreateUserCommand cmd, CancellationToken ct)
{
    return await ValidateEmail(cmd.Email)
        .Bind(email => CreateUser(email))
        .Bind(user => _repository.AddAsync(user, ct))
        .Map(user => user.Id);
}

// Endpoint maps to HTTP
await result.Match(
    onSuccess: userId => SendOkAsync(new Response(userId), ct),
    onFailure: error => SendResultAsync(Results.Problem(error.ToProblemDetails()))
);
```

---

## Error Types

```csharp
Error.Validation("Email is required");           // 400 Bad Request
Error.NotFound("User", userId);                  // 404 Not Found
Error.BusinessRule("Cannot delete active user"); // 422 Unprocessable Entity
Error.Conflict("Email already exists");          // 409 Conflict
Error.Internal("Database connection failed");    // 500 Internal Server Error
Error.External("Dynamic.xyz API unavailable");   // 502 Bad Gateway
```

---

## Related Decisions

- [ADR-002: CQRS with MediatR](./002-cqrs-mediatr.md)

---

**Last Updated**: 2025-09-29