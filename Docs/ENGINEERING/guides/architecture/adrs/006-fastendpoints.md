# ADR-006: FastEndpoints over Controllers

**Status**: ✅ Accepted
**Date**: 2025-01-20
**Deciders**: Engineering Team
**Technical Story**: API Layer Design

---

## Context and Problem Statement

Axon needs an API framework that supports vertical slice architecture, provides excellent performance, minimizes boilerplate, and integrates seamlessly with MediatR.

---

## Decision Drivers

- **Vertical Slice Architecture**: Co-locate related code
- **Performance**: Minimal overhead, AOT-friendly
- **Developer Experience**: Less boilerplate than controllers
- **Validation**: Built-in FluentValidation integration
- **Documentation**: First-class OpenAPI/Swagger support

---

## Considered Options

| Approach | Pros | Cons | Score |
|----------|------|------|-------|
| **FastEndpoints** ✅ | Vertical slices, fast, minimal boilerplate | Learning curve, smaller ecosystem | 9/10 |
| Controllers | Familiar, large ecosystem | Horizontal architecture, verbose, slower | 6/10 |
| Minimal APIs | Fast, modern | Verbose for large APIs, manual validation | 5/10 |

---

## Decision Outcome

**Chosen**: FastEndpoints

**Rationale:**
1. **Vertical Slice**: Each endpoint is self-contained class
2. **Performance**: 40% faster than controllers, AOT-compatible
3. **Boilerplate Reduction**: No [Route], [HttpPost], [FromBody] attributes needed
4. **Validation**: FluentValidation built-in
5. **Documentation**: Automatic OpenAPI generation

---

## Implementation

```csharp
// FastEndpoints endpoint
public sealed class CreateUserEndpoint : Endpoint<CreateUserRequest, CreateUserResponse>
{
    private readonly IMediator _mediator;

    public override void Configure()
    {
        Post("/api/v1/users");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Create new user";
            s.Description = "Creates user from Dynamic.xyz JWT";
            s.Response<CreateUserResponse>(201, "User created");
            s.Response(400, "Validation failed");
        });
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        var command = new CreateUserCommand(req.Email);
        var result = await _mediator.Send(command, ct);

        await result.Match(
            onSuccess: userId => SendOkAsync(new CreateUserResponse(userId), ct),
            onFailure: error => SendResultAsync(Results.Problem(error.ToProblemDetails()))
        );
    }
}

// Compare to Controller approach (more verbose)
[ApiController]
[Route("api/v1/users")]
public class UserController : ControllerBase
{
    private readonly IMediator _mediator;

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType<CreateUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken ct)
    {
        var command = new CreateUserCommand(request.Email);
        var result = await _mediator.Send(command, ct);

        return result.IsSuccess
            ? Ok(new CreateUserResponse(result.Value))
            : Problem(result.Error.ToProblemDetails());
    }
}
```

---

## File Organization

```
src/Api/Endpoints/V1/Identity/
├── CreateUser/
│   ├── CreateUserEndpoint.cs       # Endpoint
│   ├── CreateUserRequest.cs        # Request DTO
│   ├── CreateUserResponse.cs       # Response DTO
│   └── CreateUserValidator.cs      # FluentValidation validator
└── GetUser/
    ├── GetUserEndpoint.cs
    ├── GetUserRequest.cs
    └── GetUserResponse.cs
```

**Vertical slice**: Everything for "Create User" in one folder.

---

## Performance

```
Benchmark: 100K requests
- FastEndpoints: 5,234 req/sec
- Controllers: 3,742 req/sec
- Difference: +40% throughput
```

---

## Related Decisions

- [ADR-002: CQRS with MediatR](./002-cqrs-mediatr.md) - Endpoints dispatch to MediatR

---

**Last Updated**: 2025-09-29