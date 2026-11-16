using System.Text.Json;
using System.Text.Json.Serialization;
using DotNextAI.Api.Endpoints;
using DotNextAI.Api.RateLimiting;
using DotNextAI.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.TryAddSingleton(TimeProvider.System);

builder.Services.AddProblemDetails();
builder.Services.AddDotNextAIRateLimiting(builder.Configuration);

// Enums travel as camelCase strings: chat roles are "user" and "assistant".
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
builder.Services.AddOpenApi();

// The web app (Next.js, a later step) calls the API from the browser: only its origins are allowed.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .WithMethods("GET", "POST")
        .AllowAnyHeader());
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "DotNextAI API v1");
        options.DocumentTitle = "DotNextAI API";
    });
}

app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();

app.MapHealthEndpoints();
app.MapChatEndpoints();

await app.RunAsync();
