using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Axon.Api.Configuration;
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
    options.SerializerOptions.Converters.Add(new AiResponseId.AiResponseIdSystemTextJsonConverter());
    options.SerializerOptions.Converters.Add(new MessageId.MessageIdSystemTextJsonConverter());
    options.SerializerOptions.Converters.Add(new UserId.UserIdSystemTextJsonConverter());
    options.SerializerOptions.Converters.Add(new AiResponseId.AiResponseIdSystemTextJsonConverter());
    
    // Vogen VO converters  
    options.SerializerOptions.Converters.Add(new MessageContent.MessageContentSystemTextJsonConverter());
});

// Add API versioning and OData
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

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
