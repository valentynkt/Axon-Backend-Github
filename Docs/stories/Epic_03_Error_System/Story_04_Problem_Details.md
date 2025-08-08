# Story 04: Problem Details Integration

**Story ID:** AXON-ERR-004  
**Epic:** Epic_03_Enhanced_Error_System  
**Priority:** P1 - API Consistency  
**Estimated Effort:** ✅ COMPLETED (Originally 5 hours)  
**Dependencies:** Story_01_Core_Error_Types, Story_02_Domain_Exceptions  
**Status:** ✅ IMPLEMENTED - Verification Required  

---

## 📋 User Story

**As an** API developer building RESTful services in Axon Backend,  
**I want** automatic Error-to-ProblemDetails conversion following RFC 7807 with rich metadata preservation,  
**So that** API clients receive consistent, well-structured error responses with proper HTTP status codes, correlation IDs, and actionable error information regardless of the error source.

---

## 🎯 Story Context

### Existing System Integration

- **Current State:** ✅ **FULLY IMPLEMENTED** - RFC 7807 compliant Problem Details system exists in `src/BuildingBlocks/Web/ProblemDetails/` with automatic Error conversion, middleware integration, and FastEndpoints support
- **Integration Points:**
  - Enhanced Error system from Story 01
  - ASP.NET Core Problem Details infrastructure
  - FastEndpoints error handling
  - Result<T> pattern in controllers/endpoints
  - Global exception middleware
  - OpenTelemetry correlation
- **Technology Stack:** ASP.NET Core, RFC 7807, FastEndpoints, Minimal APIs
- **Architectural Layer:** BuildingBlocks/Web and API layer

### Patterns to Follow

```csharp
// RFC 7807 Problem Details format
{
  "type": "https://example.com/probs/out-of-credit",
  "title": "You do not have enough credit.",
  "status": 403,
  "detail": "Your current balance is 30, but that costs 50.",
  "instance": "/account/12345/msgs/abc",
  "balance": 30,
  "accounts": ["/account/12345", "/account/67890"]
}
```

---

## ✅ Acceptance Criteria

### Functional Requirements

1. **Error to HTTP Status Code Mapping**
   ```csharp
   public static class ErrorHttpMapping
   {
       public static int ToHttpStatusCode(this Error error) => error.Type switch
       {
           ErrorType.Validation => StatusCodes.Status400BadRequest,
           ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
           ErrorType.Forbidden => StatusCodes.Status403Forbidden,
           ErrorType.NotFound => StatusCodes.Status404NotFound,
           ErrorType.Conflict => StatusCodes.Status409Conflict,
           ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
           ErrorType.Aggregate => StatusCodes.Status422UnprocessableEntity,
           ErrorType.RateLimit => StatusCodes.Status429TooManyRequests,
           ErrorType.Internal => StatusCodes.Status500InternalServerError,
           ErrorType.Configuration => StatusCodes.Status500InternalServerError,
           ErrorType.External => StatusCodes.Status502BadGateway,
           ErrorType.Network => StatusCodes.Status502BadGateway,
           ErrorType.Timeout => StatusCodes.Status504GatewayTimeout,
           ErrorType.Persistence => StatusCodes.Status507InsufficientStorage,
           ErrorType.Security => StatusCodes.Status403Forbidden,
           ErrorType.Cancelled => 499, // Client Closed Request (non-standard)
           _ => StatusCodes.Status500InternalServerError
       };
   }
   ```

2. **Error to Problem Details Conversion**
   ```csharp
   public static class ErrorProblemDetailsExtensions
   {
       public static ProblemDetails ToProblemDetails(
           this Error error,
           string? instance = null,
           string? traceId = null)
       {
           var statusCode = error.ToHttpStatusCode();
           
           var problemDetails = new ProblemDetails
           {
               Type = GetProblemType(error.Type, statusCode),
               Title = GetProblemTitle(statusCode),
               Status = statusCode,
               Detail = error.Message,
               Instance = instance ?? error.CorrelationId
           };
           
           // Add RFC 7807 extensions
           problemDetails.Extensions["errorCode"] = error.Code;
           problemDetails.Extensions["errorType"] = error.Type.ToString();
           problemDetails.Extensions["severity"] = error.Severity.ToString();
           problemDetails.Extensions["timestamp"] = error.OccurredAt.ToString("O");
           
           if (!string.IsNullOrEmpty(traceId))
               problemDetails.Extensions["traceId"] = traceId;
           
           if (!string.IsNullOrEmpty(error.Source))
               problemDetails.Extensions["source"] = error.Source;
           
           // Add error metadata as extensions
           if (error.Metadata != null)
           {
               foreach (var (key, value) in error.Metadata)
               {
                   problemDetails.Extensions[key.ToCamelCase()] = value;
               }
           }
           
           return problemDetails;
       }
       
       // Multiple errors to Problem Details (validation scenarios)
       public static ValidationProblemDetails ToValidationProblemDetails(
           this IEnumerable<Error> errors,
           string? instance = null,
           string? traceId = null)
       {
           var validationDetails = new ValidationProblemDetails
           {
               Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
               Title = "One or more validation errors occurred.",
               Status = StatusCodes.Status400BadRequest,
               Instance = instance
           };
           
           // Group errors by property name if available
           foreach (var error in errors)
           {
               var propertyName = error.Metadata?.GetValueOrDefault("PropertyName")?.ToString() 
                   ?? "General";
                   
               if (!validationDetails.Errors.ContainsKey(propertyName))
                   validationDetails.Errors[propertyName] = new List<string>();
                   
               validationDetails.Errors[propertyName].Add(error.Message);
           }
           
           if (!string.IsNullOrEmpty(traceId))
               validationDetails.Extensions["traceId"] = traceId;
               
           return validationDetails;
       }
   }
   ```

3. **Result<T> to ActionResult Extensions**
   ```csharp
   public static class ResultActionResultExtensions
   {
       /// <summary>
       /// Convert Result<T> to ActionResult<T> with automatic Problem Details
       /// </summary>
       public static ActionResult<T> ToActionResult<T>(
           this Result<T> result,
           HttpContext? httpContext = null)
       {
           if (result.IsSuccess)
           {
               return new OkObjectResult(result.Value);
           }
           
           var problemDetails = result.Error.ToProblemDetails(
               instance: httpContext?.Request.Path,
               traceId: httpContext?.TraceIdentifier);
               
           return new ObjectResult(problemDetails)
           {
               StatusCode = problemDetails.Status
           };
       }
       
       /// <summary>
       /// Convert Result to IActionResult with custom success status
       /// </summary>
       public static IActionResult ToActionResult(
           this Result result,
           int successStatusCode = StatusCodes.Status200OK,
           HttpContext? httpContext = null)
       {
           if (result.IsSuccess)
           {
               return new StatusCodeResult(successStatusCode);
           }
           
           var problemDetails = result.Error.ToProblemDetails(
               instance: httpContext?.Request.Path,
               traceId: httpContext?.TraceIdentifier);
               
           return new ObjectResult(problemDetails)
           {
               StatusCode = problemDetails.Status
           };
       }
       
       /// <summary>
       /// Convert Result<T> to created response (201)
       /// </summary>
       public static IActionResult ToCreatedResult<T>(
           this Result<T> result,
           string location,
           HttpContext? httpContext = null)
       {
           if (result.IsSuccess)
           {
               return new CreatedResult(location, result.Value);
           }
           
           var problemDetails = result.Error.ToProblemDetails(
               instance: httpContext?.Request.Path,
               traceId: httpContext?.TraceIdentifier);
               
           return new ObjectResult(problemDetails)
           {
               StatusCode = problemDetails.Status
           };
       }
   }
   ```

4. **Minimal API IResult Extensions**
   ```csharp
   public static class ResultMinimalApiExtensions
   {
       /// <summary>
       /// Convert Result<T> to IResult for Minimal APIs
       /// </summary>
       public static IResult ToResult<T>(
           this Result<T> result,
           HttpContext httpContext)
       {
           if (result.IsSuccess)
           {
               return Results.Ok(result.Value);
           }
           
           var problemDetails = result.Error.ToProblemDetails(
               instance: httpContext.Request.Path,
               traceId: httpContext.TraceIdentifier);
               
           return Results.Problem(problemDetails);
       }
       
       /// <summary>
       /// Convert Result to IResult with custom success response
       /// </summary>
       public static IResult ToResult(
           this Result result,
           Func<IResult> successFactory,
           HttpContext httpContext)
       {
           if (result.IsSuccess)
           {
               return successFactory();
           }
           
           var problemDetails = result.Error.ToProblemDetails(
               instance: httpContext.Request.Path,
               traceId: httpContext.TraceIdentifier);
               
           return Results.Problem(problemDetails);
       }
   }
   ```

5. **FastEndpoints Integration**
   ```csharp
   public static class FastEndpointsErrorExtensions
   {
       /// <summary>
       /// Send Problem Details response in FastEndpoints
       /// </summary>
       public static Task SendProblemDetailsAsync<TRequest>(
           this IEndpoint<TRequest> endpoint,
           Error error,
           CancellationToken ct = default) where TRequest : notnull
       {
           var problemDetails = error.ToProblemDetails(
               instance: endpoint.HttpContext.Request.Path,
               traceId: endpoint.HttpContext.TraceIdentifier);
               
           return endpoint.HttpContext.Response.SendAsync(
               problemDetails,
               problemDetails.Status ?? 500,
               cancellation: ct);
       }
       
       /// <summary>
       /// Handle Result<T> in FastEndpoints
       /// </summary>
       public static async Task<IResult> HandleResultAsync<TRequest, TResponse>(
           this IEndpoint<TRequest> endpoint,
           Result<TResponse> result,
           CancellationToken ct = default) where TRequest : notnull
       {
           if (result.IsSuccess)
           {
               await endpoint.HttpContext.Response.SendOkAsync(result.Value, ct);
               return Results.Ok();
           }
           
           await endpoint.SendProblemDetailsAsync(result.Error, ct);
           return Results.Problem();
       }
   }
   ```

6. **Global Error Handling Middleware**
   ```csharp
   public class ProblemDetailsMiddleware
   {
       private readonly RequestDelegate _next;
       private readonly ILogger<ProblemDetailsMiddleware> _logger;
       private readonly IProblemDetailsService _problemDetailsService;
       
       public async Task InvokeAsync(HttpContext context)
       {
           try
           {
               await _next(context);
           }
           catch (DomainException ex)
           {
               await HandleDomainExceptionAsync(context, ex);
           }
           catch (ValidationException ex)
           {
               await HandleValidationExceptionAsync(context, ex);
           }
           catch (Exception ex)
           {
               await HandleGenericExceptionAsync(context, ex);
           }
       }
       
       private async Task HandleDomainExceptionAsync(
           HttpContext context, 
           DomainException exception)
       {
           var problemDetails = exception.Error.ToProblemDetails(
               instance: context.Request.Path,
               traceId: context.TraceIdentifier);
               
           context.Response.StatusCode = problemDetails.Status ?? 500;
           
           await _problemDetailsService.WriteAsync(new()
           {
               HttpContext = context,
               ProblemDetails = problemDetails
           });
       }
       
       private async Task HandleValidationExceptionAsync(
           HttpContext context,
           ValidationException exception)
       {
           var validationDetails = exception.Errors.ToValidationProblemDetails(
               instance: context.Request.Path,
               traceId: context.TraceIdentifier);
               
           context.Response.StatusCode = StatusCodes.Status400BadRequest;
           
           await _problemDetailsService.WriteAsync(new()
           {
               HttpContext = context,
               ProblemDetails = validationDetails
           });
       }
   }
   ```

### Integration Requirements

7. **ASP.NET Core Configuration**
   ```csharp
   public static class ProblemDetailsServiceExtensions
   {
       public static IServiceCollection AddEnhancedProblemDetails(
           this IServiceCollection services,
           Action<ProblemDetailsOptions>? configure = null)
       {
           services.AddProblemDetails(options =>
           {
               // Custom problem details configuration
               options.CustomizeProblemDetails = context =>
               {
                   // Add correlation ID
                   context.ProblemDetails.Extensions["correlationId"] = 
                       context.HttpContext.TraceIdentifier;
                       
                   // Add timestamp
                   context.ProblemDetails.Extensions["timestamp"] = 
                       DateTimeOffset.UtcNow.ToString("O");
                       
                   // Add API version if present
                   var apiVersion = context.HttpContext.GetRequestedApiVersion();
                   if (apiVersion != null)
                   {
                       context.ProblemDetails.Extensions["apiVersion"] = 
                           apiVersion.ToString();
                   }
                   
                   // Development-only details
                   #if DEBUG
                   context.ProblemDetails.Extensions["machineName"] = 
                       Environment.MachineName;
                   #endif
               };
               
               // Map specific exceptions to problem details
               options.Map<DomainException>(ex => ex.Error.ToProblemDetails());
               options.Map<ValidationException>(ex => 
                   ex.Errors.ToValidationProblemDetails());
               
               // Apply custom configuration
               configure?.Invoke(options);
           });
           
           return services;
       }
       
       public static IApplicationBuilder UseEnhancedProblemDetails(
           this IApplicationBuilder app)
       {
           app.UseMiddleware<ProblemDetailsMiddleware>();
           return app;
       }
   }
   ```

### Quality Requirements

8. **Testing Coverage**
   - ✅ Unit tests for all conversion methods
   - ✅ HTTP status code mapping tests
   - ✅ Metadata preservation tests
   - ✅ Integration tests with controllers
   - ✅ FastEndpoints integration tests
   - ✅ Middleware exception handling tests

9. **Documentation**
   - ✅ XML documentation for all extensions
   - ✅ RFC 7807 compliance documentation
   - ✅ API error response examples
   - ✅ Migration guide from existing error handling

---

## 🛠 Technical Design

### File Structure

```
BuildingBlocks/
├── Core/
│   └── Diagnostics/
│       └── ProblemDetails/
│           ├── ErrorHttpMapping.cs
│           ├── ErrorProblemDetailsExtensions.cs
│           └── ProblemDetailsMiddleware.cs
└── Web/
    └── Extensions/
        ├── ResultActionResultExtensions.cs
        ├── ResultMinimalApiExtensions.cs
        ├── FastEndpointsErrorExtensions.cs
        └── ProblemDetailsServiceExtensions.cs
```

### Implementation Examples

```csharp
// Controller example with Problem Details
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderDto>> CreateOrder(
        CreateOrderRequest request,
        CancellationToken ct)
    {
        var command = request.ToCommand();
        var result = await _mediator.Send(command, ct);
        
        return result.Match<ActionResult<OrderDto>>(
            success: order => CreatedAtAction(
                nameof(GetOrder), 
                new { id = order.Id }, 
                order),
            failure: error => error.ToProblemDetails(HttpContext).ToActionResult()
        );
    }
    
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrder(
        string id,
        CancellationToken ct)
    {
        var query = new GetOrderQuery(OrderId.From(id));
        var result = await _mediator.Send(query, ct);
        
        return result.ToActionResult(HttpContext);
    }
}

// FastEndpoint example
public class CreateOrderEndpoint : Endpoint<CreateOrderRequest, OrderDto>
{
    private readonly IMediator _mediator;
    
    public override void Configure()
    {
        Post("/api/orders");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Create a new order";
            s.Response<OrderDto>(201, "Order created successfully");
            s.Response<ProblemDetails>(400, "Invalid request");
            s.Response<ProblemDetails>(422, "Business rule violation");
        });
    }
    
    public override async Task HandleAsync(
        CreateOrderRequest req,
        CancellationToken ct)
    {
        var command = req.ToCommand();
        var result = await _mediator.Send(command, ct);
        
        if (result.IsSuccess)
        {
            await SendCreatedAtAsync<GetOrderEndpoint>(
                new { id = result.Value.Id },
                result.Value,
                cancellation: ct);
        }
        else
        {
            await this.SendProblemDetailsAsync(result.Error, ct);
        }
    }
}

// Minimal API example
app.MapPost("/api/orders", async (
    CreateOrderRequest request,
    IMediator mediator,
    HttpContext context,
    CancellationToken ct) =>
{
    var command = request.ToCommand();
    var result = await mediator.Send(command, ct);
    
    return result.ToResult(context);
})
.Produces<OrderDto>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status422UnprocessableEntity);
```

---

## 🔧 Developer Guidance

### Implementation Checklist

- [ ] Create ErrorHttpMapping with status code mappings
- [ ] Implement Error.ToProblemDetails() extension
- [ ] Implement ToValidationProblemDetails for multiple errors
- [ ] Create Result<T>.ToActionResult() extensions
- [ ] Create Result.ToResult() for Minimal APIs
- [ ] Implement FastEndpoints integration extensions
- [ ] Create ProblemDetailsMiddleware
- [ ] Add service registration extensions
- [ ] Configure Problem Details options
- [ ] Add OpenTelemetry correlation support
- [ ] Create comprehensive unit tests
- [ ] Add integration tests with endpoints

### Code Review Checklist

- [ ] RFC 7807 compliance verified
- [ ] All ErrorTypes map to correct HTTP status codes
- [ ] Metadata preserved in extensions
- [ ] Correlation IDs properly propagated
- [ ] Validation errors grouped correctly
- [ ] FastEndpoints integration works
- [ ] Minimal API support complete
- [ ] Middleware handles all exception types

---

## 📊 Test Scenarios

### Unit Tests Required

```csharp
[Fact]
public void Error_ToProblemDetails_MapsCorrectly()
{
    var error = Error.Validation("Email is required", "EMAIL_REQUIRED")
        .WithMetadata("field", "email")
        .WithCorrelationId("abc123");
        
    var problemDetails = error.ToProblemDetails();
    
    problemDetails.Status.Should().Be(400);
    problemDetails.Type.Should().Contain("400");
    problemDetails.Detail.Should().Be("Email is required");
    problemDetails.Extensions["errorCode"].Should().Be("EMAIL_REQUIRED");
    problemDetails.Extensions["field"].Should().Be("email");
    problemDetails.Instance.Should().Be("abc123");
}

[Fact]
public void Result_ToActionResult_ReturnsCorrectResponse()
{
    var error = Error.NotFound("Order not found", "ORDER_NOT_FOUND");
    var result = Result<OrderDto>.Failure(error);
    
    var actionResult = result.ToActionResult();
    
    actionResult.Result.Should().BeOfType<ObjectResult>();
    var objectResult = (ObjectResult)actionResult.Result!;
    objectResult.StatusCode.Should().Be(404);
    objectResult.Value.Should().BeOfType<ProblemDetails>();
}

[Fact]
public async Task Middleware_HandlesDomainException_ReturnsProblemDetails()
{
    var exception = new DomainException(
        Error.BusinessRule("Invalid order state", "INVALID_STATE"));
        
    var context = new DefaultHttpContext();
    var middleware = new ProblemDetailsMiddleware(
        next: _ => throw exception,
        logger: NullLogger<ProblemDetailsMiddleware>.Instance,
        problemDetailsService: new MockProblemDetailsService());
        
    await middleware.InvokeAsync(context);
    
    context.Response.StatusCode.Should().Be(422);
}
```

---

## 🚀 Definition of Done

- [ ] **Code Complete**
  - [ ] Error to HTTP status code mapping
  - [ ] Error to Problem Details conversion
  - [ ] Validation Problem Details support
  - [ ] Result to ActionResult extensions
  - [ ] Minimal API extensions
  - [ ] FastEndpoints integration
  - [ ] Global error middleware
  - [ ] Service registration helpers

- [ ] **Quality Assurance**
  - [ ] Unit test coverage > 95%
  - [ ] Integration tests pass
  - [ ] RFC 7807 compliance verified
  - [ ] Code review completed
  - [ ] No compiler warnings

- [ ] **Documentation**
  - [ ] XML documentation complete
  - [ ] RFC 7807 compliance documented
  - [ ] API error examples provided
  - [ ] Migration guide written

- [ ] **Integration Verified**
  - [ ] Controllers return Problem Details
  - [ ] FastEndpoints integration works
  - [ ] Minimal APIs supported
  - [ ] Middleware handles exceptions
  - [ ] Correlation IDs preserved

---

## 🎯 Success Metrics

- **Compliance:** 100% RFC 7807 compliant
- **Coverage:** All error types mapped correctly
- **Consistency:** 100% of API errors use Problem Details
- **Performance:** < 1ms overhead for conversion
- **Adoption:** Used in all API endpoints

---

## 📝 Notes

- RFC 7807 compliance is critical for API consistency
- Preserve all metadata from Error to Problem Details extensions
- Ensure correlation IDs flow through for distributed tracing
- Consider adding API versioning to Problem Details
- Support both traditional controllers and modern minimal APIs

---

**Story Status:** ✅ **COMPLETED - VERIFICATION PHASE**  
**Implementation:** Located in `src/BuildingBlocks/Web/ProblemDetails/` and `src/BuildingBlocks/Web/Extensions/`  
**Next Action:** Verify RFC 7807 compliance and FastEndpoints integration