using Axon.Api.Configuration;
using BuildingBlocks.Core.Abstractions.Time;
using BuildingBlocks.Web.OpenApi;
using FastEndpoints;
using FastEndpoints.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

// Initialize global clock - MUST be done exactly once at startup
Clock.Initialize(app.Services.GetRequiredService<IClock>());

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseAspnetOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Configure FastEndpoints (before MVC controllers)
app.UseFastEndpoints();

// Configure MVC controllers (existing functionality)
app.MapControllers();

// Keep existing health endpoints
app.MapGet("/", () => "OK");
app.MapGet("/health", () => "OK");

app.Run();
