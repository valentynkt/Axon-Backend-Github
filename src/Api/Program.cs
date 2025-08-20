using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Axon.Api.Configuration;
using Axon.Modules.Chat.ReadModels;
using BuildingBlocks.Core.Abstractions.Time;
using BuildingBlocks.Web.OpenApi;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using BuildingBlocks.Primitives.Ids;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddApplicationServices(builder.Configuration, builder.Environment);

// Configure JSON serialization options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    
    // StronglyTypedId converters - one per ID type
    options.SerializerOptions.Converters.Add(new BuildingBlocks.Primitives.Ids.ConversationId.SystemTextJsonConverter());
    options.SerializerOptions.Converters.Add(new BuildingBlocks.Primitives.Ids.MessageId.SystemTextJsonConverter());
    options.SerializerOptions.Converters.Add(new BuildingBlocks.Primitives.Ids.UserId.SystemTextJsonConverter());
    options.SerializerOptions.Converters.Add(new BuildingBlocks.Primitives.Ids.AiResponseId.SystemTextJsonConverter());
    
    // Vogen VO converters  
    options.SerializerOptions.Converters.Add(new Axon.BuildingBlocks.Core.Primitives.ValueObjects.MessageContent.SystemTextJsonConverter());
});

// Add API versioning and OData
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddOData(options => options.AddRouteComponents("odata/v1"));

// Configure OData with EDM model
builder.Services
    .AddControllers()
    .AddOData(options => options
        .Select()
        .Filter()
        .OrderBy()
        .SetMaxTop(100)
        .Count()
        .SkipToken() // Enable server-driven paging with $skiptoken
        .AddRouteComponents("odata/v1", GetEdmModel()));

var app = builder.Build();


// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseAspnetOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Configure OData routing
app.UseRouting();

// Configure FastEndpoints (before MVC controllers)
app.UseFastEndpoints();

// Configure MVC controllers (including OData)
app.MapControllers();

// Keep existing health endpoints
app.MapGet("/", () => "OK");
app.MapGet("/health", () => "OK");

app.Run();

// EDM Model Builder
static IEdmModel GetEdmModel()
{
    var builder = new ODataConventionModelBuilder();
    
    // Register entity sets with page size for server-driven paging
    var conversations = builder.EntitySet<ConversationReadModel>("Conversations");
    conversations.EntityType.HasKey(c => c.Id);
    conversations.EntityType.Page(50, 100); // Default page size 50, max 100
    
    var messages = builder.EntitySet<MessageReadModel>("Messages");
    messages.EntityType.HasKey(m => m.Id);
    messages.EntityType.Page(50, 100); // Default page size 50, max 100
    
    return builder.GetEdmModel();
}