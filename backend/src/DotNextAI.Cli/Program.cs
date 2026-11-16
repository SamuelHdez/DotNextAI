using System.Globalization;
using System.Text;
using DotNextAI.Application.Chat;
using DotNextAI.Application.Images;
using DotNextAI.Cli;
using DotNextAI.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// The CLI speaks English (docs/specs/feature-cli.md): System.CommandLine's own texts (help, parse errors) would
// otherwise follow the OS language. UTF-8 without BOM keeps accents intact in Windows terminals and in redirected
// output.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

// The CLI is a local development tool, so it runs in Development by default: that loads the user-secrets shared with
// the API (the LiteLLM settings).
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = [],
    EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environments.Development,
});

// stdout carries only the answers. Logs are off by default and go to stderr when enabled, e.g. with
// Logging__LogLevel__Default=Debug.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.None);

builder.Services.AddInfrastructure(builder.Configuration);

using var host = builder.Build();

return await CliApplication.RunAsync(
    args,
    host.Services.GetRequiredService<IChatService>(),
    host.Services.GetRequiredService<IModelCatalog>(),
    host.Services.GetRequiredService<IImageService>(),
    CliConsole.System(),
    TimeProvider.System);
