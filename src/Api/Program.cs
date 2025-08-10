using Axon.Api.Configuration;
using FastEndpoints;
using FastEndpoints.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddApplicationServices(builder.Configuration, builder.Environment);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Configure FastEndpoints (before MVC controllers)
app.UseFastEndpoints()
   .UseSwaggerGen();

// Configure MVC controllers (existing functionality)
app.MapControllers();

// Keep existing health endpoints
app.MapGet("/", () => "OK");
app.MapGet("/health", () => "OK");

app.Run();
