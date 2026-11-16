using DotNextAI.Infrastructure;
using DotNextAI.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Terminal chatbot (backlog steps 01-03). The CLI is a local development tool, so it runs in Development by default:
// that loads the user-secrets shared with the API.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environments.Development,
});

builder.Services.AddInfrastructure(builder.Configuration);

using var host = builder.Build();

var llm = host.Services.GetRequiredService<IOptions<LlmOptions>>().Value;
if (!llm.IsConfigured)
{
    await Console.Error.WriteLineAsync("The LiteLLM proxy is not configured. Run ./scripts/dev.sh secrets first.");
    return 1;
}

Console.WriteLine($"DotNextAI CLI ready: model '{llm.DefaultModel}' through {llm.Endpoint}");
Console.WriteLine("Questions and answers arrive with backlog step 01.");
return 0;
