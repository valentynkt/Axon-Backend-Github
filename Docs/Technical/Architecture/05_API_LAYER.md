# Part 5: API Layer - Comprehensive Implementation Guide

## Overview
The API Layer uses FastEndpoints for high-performance, minimal ceremony endpoints with built-in validation, OpenAPI documentation, and railway-oriented error handling.

## Core Principles
- **FastEndpoints**: Minimal API with REPR pattern
- **Railway-Oriented**: All endpoints handle `Result<T>` properly
- **OpenAPI First**: Full Swagger documentation
- **Security**: JWT authentication with policies
- **Versioning**: API versioning support
- **Rate Limiting**: Per-endpoint rate limits

## 1. FastEndpoints Setup

### 1.1 Program.cs Configuration

```csharp
// src/API/Program.cs
using FastEndpoints;
using FastEndpoints.Swagger;
using FastEndpoints.Security;
using Axon.Modules.Chat.Application;
using Axon.Modules.Chat.Infrastructure;
using Axon.API.Common;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console(new JsonFormatter())
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = context.Configuration["OpenTelemetry:LogsEndpoint"];
        });
});

// Add services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>())
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Add authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ChatUser", policy => policy.RequireClaim("scope", "chat.read", "chat.write"));
    options.AddPolicy("ChatAdmin", policy => policy.RequireClaim("role", "admin"));
});

// Add FastEndpoints
builder.Services
    .AddFastEndpoints(options =>
    {
        options.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All;
    })
    .SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.Title = "Axon Chat API";
            s.Version = "v1";
            s.Description = "Chat module API with AI integration";
            
            s.AddAuth("Bearer", new()
            {
                Type = OpenApiSecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT",
                Description = "JWT Authorization header using the Bearer scheme"
            });
        };
        
        o.ShortSchemaNames = true;
        o.AutoTagPathSegmentIndex = 2;
    })
    .AddResponseCaching()
    .AddRateLimiting();

// Add modules
builder.Services.AddChatApplication(builder.Configuration);
builder.Services.AddChatInfrastructure(builder.Configuration);

// Add common services
builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<GlobalExceptionHandler>();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ChatDbContext>("database")
    .AddRedis("redis")
    .AddElasticsearch("elasticsearch");

var app = builder.Build();

// Middleware pipeline
app.UseAuthentication();
app.UseAuthorization();
app.UseCors("AllowSpecificOrigins");
app.UseResponseCaching();
app.UseRateLimiter();

// FastEndpoints
app.UseFastEndpoints(config =>
{
    config.Errors.ResponseBuilder = (failures, ctx, statusCode) =>
    {
        return new ValidationProblemDetails(failures, ctx, statusCode);
    };
    
    config.Versioning.Prefix = "v";
    config.Versioning.DefaultVersion = 1;
    config.Versioning.PrependToRoute = true;
    
    config.Endpoints.RoutePrefix = "api";
    config.Endpoints.Configurator = ep =>
    {
        ep.PreProcessor<RequestLoggingPreProcessor>(Order.Before);
        ep.PostProcessor<ResponseLoggingPostProcessor>(Order.After);
    };
});

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();
}

// Health checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

await app.RunAsync();
```

## 2. Common API Infrastructure

### 2.1 Base Endpoint

```csharp
// src/API/Common/BaseEndpoint.cs
using FastEndpoints;
using BuildingBlocks.Core.Results;
using FluentValidation.Results;

namespace Axon.API.Common;

public abstract class BaseEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected async Task<IResult> HandleResult<T>(Result<T> result, Func<T, object>? mapper = null)
    {
        if (result.IsSuccess)
        {
            var response = mapper != null ? mapper(result.Value) : result.Value;
            return TypedResults.Ok(response);
        }

        return MapErrorToHttpResult(result.Error);
    }

    protected IResult MapErrorToHttpResult(Error error)
    {
        return error.Type switch
        {
            ErrorType.Validation => TypedResults.BadRequest(CreateProblemDetails(error, 400)),
            ErrorType.NotFound => TypedResults.NotFound(CreateProblemDetails(error, 404)),
            ErrorType.Conflict => TypedResults.Conflict(CreateProblemDetails(error, 409)),
            ErrorType.Forbidden => TypedResults.Forbid(),
            ErrorType.Unauthorized => TypedResults.Unauthorized(),
            ErrorType.TooManyRequests => TypedResults.StatusCode(429),
            ErrorType.Unavailable => TypedResults.StatusCode(503),
            _ => TypedResults.StatusCode(500)
        };
    }

    private static ProblemDetails CreateProblemDetails(Error error, int statusCode)
    {
        return new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{statusCode}",
            Title = error.Code,
            Status = statusCode,
            Detail = error.Message,
            Extensions = error.Metadata?.ToDictionary(kvp => (string)kvp.Key, kvp => kvp.Value)
        };
    }

    protected string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? throw new UnauthorizedAccessException("User ID not found in token");
    }
}

// src/API/Common/ValidationProblemDetails.cs
public sealed class ValidationProblemDetails : ProblemDetails
{
    public Dictionary<string, string[]> Errors { get; set; } = new();

    public ValidationProblemDetails(
        List<ValidationFailure> failures,
        HttpContext context,
        int statusCode)
    {
        Type = "https://httpstatuses.io/400";
        Title = "Validation Failed";
        Status = statusCode;
        Instance = context.Request.Path;
        
        Errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray());
    }
}
```

### 2.2 Request/Response Processors

```csharp
// src/API/Common/RequestLoggingPreProcessor.cs
namespace Axon.API.Common;

public sealed class RequestLoggingPreProcessor : IPreProcessor<object>
{
    public async Task PreProcessAsync(object req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        using var activity = Activity.StartActivity("API.Request");
        activity?.SetTag("http.request.method", ctx.Request.Method);
        activity?.SetTag("http.request.path", ctx.Request.Path);
        activity?.SetTag("user.id", ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        
        var logger = ctx.Resolve<ILogger<RequestLoggingPreProcessor>>();
        logger.LogInformation(
            "Processing {Method} {Path} for user {UserId}",
            ctx.Request.Method,
            ctx.Request.Path,
            ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }
}

// src/API/Common/ResponseLoggingPostProcessor.cs
public sealed class ResponseLoggingPostProcessor : IPostProcessor<object, object>
{
    public async Task PostProcessAsync(object req, object res, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct)
    {
        var activity = Activity.Current;
        activity?.SetTag("http.response.status_code", ctx.Response.StatusCode);
        
        var logger = ctx.Resolve<ILogger<ResponseLoggingPostProcessor>>();
        logger.LogInformation(
            "Completed {Method} {Path} with status {StatusCode}",
            ctx.Request.Method,
            ctx.Request.Path,
            ctx.Response.StatusCode);
    }
}
```

## 3. Conversation Endpoints

### 3.1 Start Conversation Endpoint

```csharp
// src/API/Endpoints/Conversations/StartConversationEndpoint.cs
using FastEndpoints;
using MediatR;
using Axon.Modules.Chat.Application.Commands.StartConversation;

namespace Axon.API.Endpoints.Conversations;

public sealed class StartConversationRequest
{
    public string Title { get; set; } = string.Empty;
    public string InitialMessage { get; set; } = string.Empty;
    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class StartConversationResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
}

public sealed class StartConversationValidator : Validator<StartConversationRequest>
{
    public StartConversationValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MinimumLength(1).WithMessage("Title must be at least 1 character")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.InitialMessage)
            .NotEmpty().WithMessage("Initial message is required")
            .MaximumLength(4000).WithMessage("Message must not exceed 4000 characters");
    }
}

public sealed class StartConversationEndpoint : BaseEndpoint<StartConversationRequest, StartConversationResponse>
{
    private readonly IMediator _mediator;

    public StartConversationEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/conversations");
        Policies("ChatUser");
        Summary(s =>
        {
            s.Summary = "Start a new conversation";
            s.Description = "Creates a new conversation with an initial message";
            s.Response<StartConversationResponse>(201, "Conversation created successfully");
            s.Response<ProblemDetails>(400, "Validation failed");
            s.Response(401, "Unauthorized");
        });
        Options(x => x
            .WithTags("Conversations")
            .ProducesProblemDetails());
    }

    public override async Task HandleAsync(StartConversationRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(GetUserId());
        
        var command = new StartConversationCommand
        {
            Title = req.Title,
            UserId = userId,
            InitialMessage = req.InitialMessage,
            Metadata = req.Metadata
        };

        var result = await _mediator.Send(command, ct);

        if (result.IsSuccess)
        {
            var response = new StartConversationResponse
            {
                Id = result.Value.Id,
                Title = result.Value.Title,
                Status = result.Value.Status,
                StartedAt = result.Value.StartedAt
            };

            await SendCreatedAtAsync<GetConversationEndpoint>(
                new { ConversationId = result.Value.Id },
                response,
                cancellation: ct);
        }
        else
        {
            await SendResultAsync(MapErrorToHttpResult(result.Error));
        }
    }
}
```

### 3.2 Send Message Endpoint

```csharp
// src/API/Endpoints/Conversations/SendMessageEndpoint.cs
namespace Axon.API.Endpoints.Conversations;

public sealed class SendMessageRequest
{
    [FromRoute] public Guid ConversationId { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string>? Attachments { get; set; }
}

public sealed class SendMessageResponse
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

public sealed class SendMessageValidator : Validator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required")
            .MaximumLength(4000).WithMessage("Message must not exceed 4000 characters");

        RuleForEach(x => x.Attachments)
            .Must(BeValidUrl).WithMessage("Invalid attachment URL");
    }

    private bool BeValidUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}

public sealed class SendMessageEndpoint : BaseEndpoint<SendMessageRequest, SendMessageResponse>
{
    private readonly IMediator _mediator;

    public SendMessageEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/conversations/{ConversationId}/messages");
        Policies("ChatUser");
        Summary(s =>
        {
            s.Summary = "Send a message to a conversation";
            s.Description = "Adds a new message to an existing conversation";
            s.Response<SendMessageResponse>(200, "Message sent successfully");
            s.Response<ProblemDetails>(400, "Validation failed");
            s.Response<ProblemDetails>(404, "Conversation not found");
        });
        Options(x => x
            .WithTags("Conversations", "Messages")
            .ProducesProblemDetails());
    }

    public override async Task HandleAsync(SendMessageRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(GetUserId());

        var command = new SendMessageCommand
        {
            ConversationId = req.ConversationId,
            UserId = userId,
            Content = req.Content,
            Role = MessageRole.User,
            Attachments = req.Attachments
        };

        var result = await _mediator.Send(command, ct);

        if (result.IsSuccess)
        {
            var response = new SendMessageResponse
            {
                Id = result.Value.Id,
                Content = result.Value.Content,
                Role = result.Value.Role,
                SentAt = result.Value.SentAt
            };

            await SendOkAsync(response, ct);
        }
        else
        {
            await SendResultAsync(MapErrorToHttpResult(result.Error));
        }
    }
}
```

### 3.3 Process AI Response Endpoint

```csharp
// src/API/Endpoints/Conversations/ProcessAiResponseEndpoint.cs
namespace Axon.API.Endpoints.Conversations;

public sealed class ProcessAiResponseRequest
{
    [FromRoute] public Guid ConversationId { get; set; }
    public string UserMessage { get; set; } = string.Empty;
    public string Model { get; set; } = "GPT4";
    public Dictionary<string, object>? Parameters { get; set; }
}

public sealed class ProcessAiResponseResponse
{
    public string Content { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public long ProcessingTimeMs { get; set; }
}

public sealed class ProcessAiResponseEndpoint : BaseEndpoint<ProcessAiResponseRequest, ProcessAiResponseResponse>
{
    private readonly IMediator _mediator;

    public ProcessAiResponseEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/conversations/{ConversationId}/ai");
        Policies("ChatUser");
        Throttle(
            hitLimit: 10,
            window: TimeSpan.FromMinutes(1),
            headerName: "X-Rate-Limit");
        Summary(s =>
        {
            s.Summary = "Process AI response for a conversation";
            s.Description = "Sends a message and gets an AI-generated response";
            s.Response<ProcessAiResponseResponse>(200, "AI response generated successfully");
            s.Response<ProblemDetails>(429, "Rate limit exceeded");
        });
        Options(x => x
            .WithTags("Conversations", "AI")
            .ProducesProblemDetails());
    }

    public override async Task HandleAsync(ProcessAiResponseRequest req, CancellationToken ct)
    {
        var command = new ProcessAiResponseCommand
        {
            ConversationId = req.ConversationId,
            UserMessage = req.UserMessage,
            Model = Enum.Parse<AiModel>(req.Model),
            Parameters = req.Parameters
        };

        var result = await _mediator.Send(command, ct);

        if (result.IsSuccess)
        {
            var response = new ProcessAiResponseResponse
            {
                Content = result.Value.Content,
                Model = result.Value.Model,
                TokensUsed = result.Value.TokensUsed,
                ProcessingTimeMs = result.Value.ProcessingTimeMs
            };

            await SendOkAsync(response, ct);
        }
        else
        {
            await SendResultAsync(MapErrorToHttpResult(result.Error));
        }
    }
}
```

### 3.4 Get Conversation Endpoint

```csharp
// src/API/Endpoints/Conversations/GetConversationEndpoint.cs
namespace Axon.API.Endpoints.Conversations;

public sealed class GetConversationRequest
{
    [FromRoute] public Guid ConversationId { get; set; }
    [FromQuery] public bool IncludeMessages { get; set; } = true;
}

public sealed class GetConversationResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public List<MessageDto> Messages { get; set; } = new();
    public List<ParticipantDto> Participants { get; set; } = new();
    public TokenUsageDto? TokenUsage { get; set; }
}

public sealed class GetConversationEndpoint : BaseEndpoint<GetConversationRequest, GetConversationResponse>
{
    private readonly IMediator _mediator;

    public GetConversationEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/conversations/{ConversationId}");
        Policies("ChatUser");
        ResponseCache(60); // Cache for 60 seconds
        Summary(s =>
        {
            s.Summary = "Get conversation details";
            s.Description = "Retrieves a conversation with optional message history";
            s.Response<GetConversationResponse>(200, "Conversation retrieved successfully");
            s.Response<ProblemDetails>(404, "Conversation not found");
        });
        Options(x => x
            .WithTags("Conversations")
            .ProducesProblemDetails());
    }

    public override async Task HandleAsync(GetConversationRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(GetUserId());

        var query = new GetConversationQuery
        {
            ConversationId = req.ConversationId,
            UserId = userId,
            IncludeMessages = req.IncludeMessages
        };

        var result = await _mediator.Send(query, ct);

        if (result.IsSuccess)
        {
            var response = MapToResponse(result.Value);
            await SendOkAsync(response, ct);
        }
        else
        {
            await SendResultAsync(MapErrorToHttpResult(result.Error));
        }
    }

    private GetConversationResponse MapToResponse(ConversationDetailDto dto)
    {
        return new GetConversationResponse
        {
            Id = dto.Id,
            Title = dto.Title,
            Status = dto.Status,
            StartedAt = dto.StartedAt,
            LastMessageAt = dto.LastMessageAt,
            Messages = dto.Messages,
            Participants = dto.Participants,
            TokenUsage = dto.TokenUsage
        };
    }
}
```

### 3.5 List User Conversations Endpoint

```csharp
// src/API/Endpoints/Conversations/ListUserConversationsEndpoint.cs
namespace Axon.API.Endpoints.Conversations;

public sealed class ListUserConversationsRequest
{
    [FromQuery] public int PageNumber { get; set; } = 1;
    [FromQuery] public int PageSize { get; set; } = 20;
    [FromQuery] public string? Status { get; set; }
    [FromQuery] public DateTime? Since { get; set; }
    [FromQuery] public string? SortBy { get; set; } = "LastMessageAt";
    [FromQuery] public bool Descending { get; set; } = true;
}

public sealed class ListUserConversationsResponse
{
    public List<ConversationSummaryDto> Items { get; set; } = new();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public sealed class ListUserConversationsValidator : Validator<ListUserConversationsRequest>
{
    public ListUserConversationsValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.Status)
            .Must(BeValidStatus).When(x => !string.IsNullOrEmpty(x.Status))
            .WithMessage("Invalid conversation status");

        RuleFor(x => x.SortBy)
            .Must(BeValidSortField).When(x => !string.IsNullOrEmpty(x.SortBy))
            .WithMessage("Invalid sort field");
    }

    private bool BeValidStatus(string? status)
    {
        return Enum.TryParse<ConversationStatus>(status, out _);
    }

    private bool BeValidSortField(string? field)
    {
        var validFields = new[] { "Title", "StartedAt", "LastMessageAt" };
        return validFields.Contains(field, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class ListUserConversationsEndpoint : BaseEndpoint<ListUserConversationsRequest, ListUserConversationsResponse>
{
    private readonly IMediator _mediator;

    public ListUserConversationsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/conversations");
        Policies("ChatUser");
        ResponseCache(30); // Cache for 30 seconds
        Summary(s =>
        {
            s.Summary = "List user conversations";
            s.Description = "Retrieves a paginated list of conversations for the current user";
            s.Response<ListUserConversationsResponse>(200, "Conversations retrieved successfully");
        });
        Options(x => x
            .WithTags("Conversations")
            .ProducesProblemDetails());
    }

    public override async Task HandleAsync(ListUserConversationsRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(GetUserId());

        var query = new ListUserConversationsQuery
        {
            UserId = userId,
            PageNumber = req.PageNumber,
            PageSize = req.PageSize,
            Status = string.IsNullOrEmpty(req.Status) ? null : Enum.Parse<ConversationStatus>(req.Status),
            Since = req.Since,
            SortBy = req.SortBy,
            Descending = req.Descending
        };

        var result = await _mediator.Send(query, ct);

        if (result.IsSuccess)
        {
            var response = new ListUserConversationsResponse
            {
                Items = result.Value.Items,
                PageNumber = result.Value.PageNumber,
                PageSize = result.Value.PageSize,
                TotalCount = result.Value.TotalCount,
                TotalPages = result.Value.TotalPages
            };

            await SendOkAsync(response, ct);
        }
        else
        {
            await SendResultAsync(MapErrorToHttpResult(result.Error));
        }
    }
}
```

### 3.6 Search Conversations Endpoint

```csharp
// src/API/Endpoints/Conversations/SearchConversationsEndpoint.cs
namespace Axon.API.Endpoints.Conversations;

public sealed class SearchConversationsRequest
{
    [FromQuery] public string SearchTerm { get; set; } = string.Empty;
    [FromQuery] public List<string>? StatusFilters { get; set; }
    [FromQuery] public DateTime? DateFrom { get; set; }
    [FromQuery] public DateTime? DateTo { get; set; }
    [FromQuery] public int MaxResults { get; set; } = 50;
}

public sealed class SearchConversationsResponse
{
    public string Query { get; set; } = string.Empty;
    public int TotalHits { get; set; }
    public List<ConversationSearchResultDto> Results { get; set; } = new();
    public Dictionary<string, List<FacetDto>>? Facets { get; set; }
    public long ExecutionTimeMs { get; set; }
}

public sealed class ConversationSearchResultDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string MatchedContent { get; set; } = string.Empty;
    public List<string> Highlights { get; set; } = new();
    public float Score { get; set; }
}

public sealed class FacetDto
{
    public string Value { get; set; } = string.Empty;
    public long Count { get; set; }
}

public sealed class SearchConversationsEndpoint : BaseEndpoint<SearchConversationsRequest, SearchConversationsResponse>
{
    private readonly IMediator _mediator;

    public SearchConversationsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/conversations/search");
        Policies("ChatUser");
        Summary(s =>
        {
            s.Summary = "Search conversations";
            s.Description = "Full-text search across user's conversations";
            s.Response<SearchConversationsResponse>(200, "Search results retrieved successfully");
        });
        Options(x => x
            .WithTags("Conversations", "Search")
            .ProducesProblemDetails());
    }

    public override async Task HandleAsync(SearchConversationsRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(GetUserId());

        var query = new SearchConversationsQuery
        {
            SearchTerm = req.SearchTerm,
            UserId = userId,
            StatusFilters = req.StatusFilters?.Select(s => Enum.Parse<ConversationStatus>(s)).ToList(),
            DateFrom = req.DateFrom,
            DateTo = req.DateTo,
            MaxResults = req.MaxResults
        };

        var result = await _mediator.Send(query, ct);

        if (result.IsSuccess)
        {
            var response = new SearchConversationsResponse
            {
                Query = result.Value.Query,
                TotalHits = result.Value.TotalHits,
                Results = result.Value.Results.Select(r => new ConversationSearchResultDto
                {
                    Id = r.Id,
                    Title = r.Title,
                    Status = r.Status,
                    MatchedContent = r.MatchedContent,
                    Highlights = r.Highlights,
                    Score = r.Score
                }).ToList(),
                Facets = result.Value.Facets?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(v => new FacetDto 
                    { 
                        Value = v.Value, 
                        Count = v.Count 
                    }).ToList()),
                ExecutionTimeMs = result.Value.ExecutionTimeMs
            };

            await SendOkAsync(response, ct);
        }
        else
        {
            await SendResultAsync(MapErrorToHttpResult(result.Error));
        }
    }
}
```

### 3.7 Archive Conversation Endpoint

```csharp
// src/API/Endpoints/Conversations/ArchiveConversationEndpoint.cs
namespace Axon.API.Endpoints.Conversations;

public sealed class ArchiveConversationRequest
{
    [FromRoute] public Guid ConversationId { get; set; }
    public string? Reason { get; set; }
}

public sealed class ArchiveConversationEndpoint : BaseEndpoint<ArchiveConversationRequest, EmptyResponse>
{
    private readonly IMediator _mediator;

    public ArchiveConversationEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/conversations/{ConversationId}/archive");
        Policies("ChatUser");
        Summary(s =>
        {
            s.Summary = "Archive a conversation";
            s.Description = "Archives a conversation, making it read-only";
            s.Response(204, "Conversation archived successfully");
            s.Response<ProblemDetails>(404, "Conversation not found");
            s.Response<ProblemDetails>(409, "Conversation already archived");
        });
        Options(x => x
            .WithTags("Conversations")
            .ProducesProblemDetails());
    }

    public override async Task HandleAsync(ArchiveConversationRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(GetUserId());

        var command = new ArchiveConversationCommand
        {
            ConversationId = req.ConversationId,
            UserId = userId,
            Reason = req.Reason
        };

        var result = await _mediator.Send(command, ct);

        if (result.IsSuccess)
        {
            await SendNoContentAsync(ct);
        }
        else
        {
            await SendResultAsync(MapErrorToHttpResult(result.Error));
        }
    }
}
```

## 4. WebSocket Support for Real-time

### 4.1 SignalR Hub

```csharp
// src/API/Hubs/ChatHub.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace Axon.API.Hubs;

[Authorize(Policy = "ChatUser")]
public sealed class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;
    private readonly IConnectionManager _connectionManager;

    public ChatHub(ILogger<ChatHub> logger, IConnectionManager connectionManager)
    {
        _logger = logger;
        _connectionManager = connectionManager;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await _connectionManager.AddConnectionAsync(userId, Context.ConnectionId);
            _logger.LogInformation("User {UserId} connected with {ConnectionId}", userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await _connectionManager.RemoveConnectionAsync(userId, Context.ConnectionId);
            _logger.LogInformation("User {UserId} disconnected from {ConnectionId}", userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
        _logger.LogInformation("User {UserId} joined conversation {ConversationId}", 
            Context.UserIdentifier, conversationId);
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
        _logger.LogInformation("User {UserId} left conversation {ConversationId}", 
            Context.UserIdentifier, conversationId);
    }

    public async Task SendTypingIndicator(Guid conversationId, bool isTyping)
    {
        await Clients.OthersInGroup($"conversation-{conversationId}")
            .SendAsync("UserTyping", new
            {
                UserId = Context.UserIdentifier,
                ConversationId = conversationId,
                IsTyping = isTyping
            });
    }
}

// src/API/Hubs/ConnectionManager.cs
public interface IConnectionManager
{
    Task AddConnectionAsync(string userId, string connectionId);
    Task RemoveConnectionAsync(string userId, string connectionId);
    Task<IEnumerable<string>> GetConnectionsAsync(string userId);
}

public sealed class ConnectionManager : IConnectionManager
{
    private readonly ICacheService _cache;
    private const string KeyPrefix = "signalr:connections:";

    public ConnectionManager(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task AddConnectionAsync(string userId, string connectionId)
    {
        var key = $"{KeyPrefix}{userId}";
        var connections = await GetConnectionsAsync(userId);
        var updated = connections.Append(connectionId).Distinct();
        await _cache.SetAsync(key, updated, TimeSpan.FromHours(24), CancellationToken.None);
    }

    public async Task RemoveConnectionAsync(string userId, string connectionId)
    {
        var key = $"{KeyPrefix}{userId}";
        var connections = await GetConnectionsAsync(userId);
        var updated = connections.Where(c => c != connectionId);
        await _cache.SetAsync(key, updated, TimeSpan.FromHours(24), CancellationToken.None);
    }

    public async Task<IEnumerable<string>> GetConnectionsAsync(string userId)
    {
        var key = $"{KeyPrefix}{userId}";
        var cached = await _cache.GetAsync<IEnumerable<string>>(key, CancellationToken.None);
        return cached.IsSome ? cached.Value : Enumerable.Empty<string>();
    }
}
```

### 4.2 Real-time Event Handlers

```csharp
// src/API/EventHandlers/MessageAddedEventHandler.cs
using BuildingBlocks.Core.Events;
using Microsoft.AspNetCore.SignalR;
using Axon.Modules.Chat.Domain.Conversation.Events;

namespace Axon.API.EventHandlers;

public sealed class MessageAddedEventHandler : IDomainEventHandler<MessageAddedEvent>
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<MessageAddedEventHandler> _logger;

    public MessageAddedEventHandler(
        IHubContext<ChatHub> hubContext,
        ILogger<MessageAddedEventHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task HandleAsync(MessageAddedEvent @event, CancellationToken cancellationToken)
    {
        var notification = new
        {
            ConversationId = @event.ConversationId.Value,
            MessageId = @event.MessageId.Value,
            UserId = @event.UserId,
            Content = @event.Content,
            Role = @event.Role,
            Timestamp = @event.OccurredOn
        };

        await _hubContext.Clients
            .Group($"conversation-{@event.ConversationId.Value}")
            .SendAsync("MessageReceived", notification, cancellationToken);

        _logger.LogInformation(
            "Sent real-time notification for message {MessageId} in conversation {ConversationId}",
            @event.MessageId.Value, @event.ConversationId.Value);
    }
}
```

## 5. Health Checks

```csharp
// src/API/HealthChecks/OpenAiHealthCheck.cs
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Axon.API.HealthChecks;

public sealed class OpenAiHealthCheck : IHealthCheck
{
    private readonly IAiService _aiService;
    private readonly ILogger<OpenAiHealthCheck> _logger;

    public OpenAiHealthCheck(IAiService aiService, ILogger<OpenAiHealthCheck> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var testContext = new ConversationContext
            {
                Messages = new List<ContextMessage>
                {
                    new() { Role = "user", Content = "Test", Timestamp = DateTime.UtcNow }
                },
                SystemPrompt = "You are a test assistant",
                MaxTokens = 10
            };

            var result = await _aiService.GenerateResponseAsync(
                testContext,
                AiModel.GPT35Turbo,
                new Dictionary<string, object> { ["max_tokens"] = 5 },
                cancellationToken);

            if (result.IsSuccess)
            {
                return HealthCheckResult.Healthy("OpenAI service is responsive");
            }

            return HealthCheckResult.Unhealthy(
                "OpenAI service returned failure",
                data: new Dictionary<string, object> { ["error"] = result.Error.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI health check failed");
            return HealthCheckResult.Unhealthy("OpenAI service is not available", ex);
        }
    }
}
```

## 6. API Documentation

### 6.1 OpenAPI Extensions

```csharp
// src/API/Documentation/OpenApiConfigurationExtensions.cs
namespace Axon.API.Documentation;

public static class OpenApiConfigurationExtensions
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.SwaggerDocument(o =>
        {
            o.MaxEndpointVersion = 1;
            o.DocumentSettings = s =>
            {
                s.DocumentName = "Axon Chat API";
                s.Title = "Axon Chat API";
                s.Version = "v1";
                s.Description = @"
# Axon Chat API

High-performance chat API with AI integration built on FastEndpoints.

## Features
- Real-time messaging via SignalR
- AI-powered responses (GPT-4, Claude)
- Full-text search with Elasticsearch
- Rate limiting and caching
- OpenTelemetry observability

## Authentication
All endpoints require JWT Bearer authentication. Include the token in the Authorization header:
```
Authorization: Bearer <your-token>
```

## Rate Limiting
AI endpoints are rate-limited to 10 requests per minute per user.

## WebSocket Support
Connect to `/chat` hub for real-time updates.
";
                
                s.AddAuth("Bearer", new()
                {
                    Type = OpenApiSecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Authorization header using the Bearer scheme"
                });

                s.ExternalDocs = new()
                {
                    Description = "GitHub Repository",
                    Url = "https://github.com/axon/chat-api"
                };
            };

            o.ShortSchemaNames = true;
            o.AutoTagPathSegmentIndex = 2;
            o.TagDescriptions = t =>
            {
                t["Conversations"] = "Conversation management endpoints";
                t["Messages"] = "Message operations within conversations";
                t["AI"] = "AI-powered response generation";
                t["Search"] = "Full-text search capabilities";
            };
        });

        return services;
    }
}
```

## 7. Testing Support

### 7.1 Integration Test Base

```csharp
// tests/API.IntegrationTests/IntegrationTestBase.cs
using FastEndpoints.Testing;
using Microsoft.AspNetCore.Hosting;

namespace Axon.API.IntegrationTests;

public abstract class IntegrationTestBase : AppFixture<Program>
{
    protected override void ConfigureApp(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Replace real services with test doubles
            services.RemoveAll<IAiService>();
            services.AddSingleton<IAiService, MockAiService>();
            
            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService, InMemoryCacheService>();
        });
    }

    protected override async Task SetupAsync()
    {
        // Seed test data
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    protected override async Task TearDownAsync()
    {
        // Clean up test data
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
    }

    protected string GenerateJwtToken(Guid userId, string[] scopes)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString())
        };

        claims.AddRange(scopes.Select(s => new Claim("scope", s)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-key-for-integration-tests"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### 7.2 Endpoint Tests

```csharp
// tests/API.IntegrationTests/Endpoints/StartConversationEndpointTests.cs
namespace Axon.API.IntegrationTests.Endpoints;

public sealed class StartConversationEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task Should_Create_Conversation_Successfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = GenerateJwtToken(userId, new[] { "chat.write" });
        
        var request = new StartConversationRequest
        {
            Title = "Test Conversation",
            InitialMessage = "Hello, this is a test message"
        };

        // Act
        var (response, result) = await Client
            .WithHeader("Authorization", $"Bearer {token}")
            .POSTAsync<StartConversationEndpoint, StartConversationRequest, StartConversationResponse>(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result.Title.Should().Be(request.Title);
        result.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Should_Return_Validation_Error_For_Empty_Title()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = GenerateJwtToken(userId, new[] { "chat.write" });
        
        var request = new StartConversationRequest
        {
            Title = "",
            InitialMessage = "Test message"
        };

        // Act
        var response = await Client
            .WithHeader("Authorization", $"Bearer {token}")
            .POSTAsync<StartConversationEndpoint, StartConversationRequest>(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("Title");
    }
}
```

## Summary

This API Layer implementation provides:

1. **FastEndpoints Setup**: Complete configuration with all middleware
2. **Base Infrastructure**: Common endpoint base class with Result handling
3. **All CRUD Endpoints**: Start, send, get, list, search, archive conversations
4. **AI Integration**: Process AI responses with rate limiting
5. **Real-time Support**: SignalR hub for WebSocket connections
6. **Health Checks**: Database, Redis, Elasticsearch, OpenAI monitoring
7. **OpenAPI Documentation**: Comprehensive Swagger docs
8. **Testing Support**: Integration test base with JWT generation

The API layer is fully railway-oriented, handling all `Result<T>` responses properly with appropriate HTTP status codes and problem details.