using System.Text.Json;
using System.Text.Json.Serialization;
using DotNextAI.Api.Endpoints;
using DotNextAI.Api.RateLimiting;
using DotNextAI.Infrastructure;
using DotNextAI.Persistence;
using Microsoft.Extensions.DependencyInjection.Extensions;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration, builder.Environment.ContentRootPath);
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
        // PATCH, PUT and DELETE: renaming, moving and deleting saved chats and folders.
        .WithMethods("GET", "POST", "PATCH", "PUT", "DELETE")
        .AllowAnyHeader());
});

var app = builder.Build();

// A local app with a SQLite file: the schema follows the code at startup (Database:MigrateOnStartup, on by default).
if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
{
    await app.Services.MigrateChatsDatabaseAsync();
}

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
app.MapModelsEndpoints();
app.MapImageEndpoints();
app.MapConversationEndpoints();

await app.RunAsync();
