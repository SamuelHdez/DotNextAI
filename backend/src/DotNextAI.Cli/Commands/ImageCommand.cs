using System.CommandLine;
using System.Globalization;
using DotNextAI.Application.Chat;
using DotNextAI.Application.Images;

namespace DotNextAI.Cli.Commands;

/// <summary>Backlog step 09: generates an image and saves it to a file; stdout gets the file's path.</summary>
internal sealed class ImageCommand(IImageService images, CliConsole console, TimeProvider timeProvider)
{
    public const string Name = "image";

    public Command Build()
    {
        var prompt = new Argument<string[]>("description")
        {
            Description = "What to draw. Quotes are optional.",
            Arity = ArgumentArity.OneOrMore,
        };
        var size = new Option<ImageSize>("--size", "-s")
        {
            Description = "Shape: square (1024×1024, default), landscape (1536×1024) or portrait (1024×1536).",
            DefaultValueFactory = _ => ImageSize.Square,
        };
        var output = new Option<string?>("--output", "-o")
        {
            Description = "File to write (default: dotnextai-image-<date>-<time>.png in the current folder).",
        };
        var force = new Option<bool>("--force") { Description = "Overwrite the output file if it exists." };

        var command = new Command(Name, "Generate an image with the proxy's image model and save it to a file.");
        command.Arguments.Add(prompt);
        command.Options.Add(size);
        command.Options.Add(output);
        command.Options.Add(force);
        command.SetAction((parseResult, cancellationToken) => RunAsync(
            string.Join(' ', parseResult.GetValue(prompt) ?? []),
            parseResult.GetValue(size),
            parseResult.GetValue(output),
            parseResult.GetValue(force),
            cancellationToken));
        return command;
    }

    public async Task<int> RunAsync(string description, ImageSize size, string? output, bool force, CancellationToken cancellationToken)
    {
        if (!images.IsConfigured)
        {
            await console.Error.WriteLineAsync("Image generation is not configured. Run ./scripts/dev.sh secrets first (Llm:ImageModel defaults to gpt-image-2).");
            return ExitCodes.NotConfigured;
        }

        if (!ImageRules.TryNormalizePrompt(description, out var prompt, out var error))
        {
            await console.Error.WriteLineAsync(error);
            return ExitCodes.UsageError;
        }

        // Checked before the (slow, billed) generation, so an existing file is not lost after paying for the image.
        var path = output?.Trim().Trim('"') is { Length: > 0 } chosen
            ? chosen
            : $"dotnextai-image-{timeProvider.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.png";
        if (File.Exists(path) && !force)
        {
            await console.Error.WriteLineAsync($"{path} already exists. Use --force to overwrite it, or --output for another file.");
            return ExitCodes.UsageError;
        }

        try
        {
            await console.Error.WriteLineAsync($"Generating a {size.ToString().ToLowerInvariant()} image with {images.Model}… (this can take a minute)");
            var image = await images.GenerateAsync(new ImageRequest(prompt, size), cancellationToken);

            path = WithExtensionFor(path, image.Image.MediaType);
            await File.WriteAllBytesAsync(path, image.Image.Data.ToArray(), cancellationToken);
            await console.Out.WriteLineAsync(Path.GetFullPath(path));
            if (image.RevisedPrompt is { } revised)
            {
                await console.Error.WriteLineAsync($"The model drew: {revised}");
            }

            await CliMessages.WriteUsageAsync(console.Error, image.Usage);

            return ExitCodes.Success;
        }
        catch (ChatServiceException ex)
        {
            await CliMessages.WriteFailureAsync(console.Error, ex);
            return ex.Failure == ChatFailure.NotConfigured ? ExitCodes.NotConfigured : ExitCodes.ModelError;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await console.Error.WriteLineAsync($"The image could not be saved to {path}: {ex.Message}");
            return ExitCodes.ModelError;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await console.Error.WriteLineAsync(CliMessages.Cancelled);
            return ExitCodes.Cancelled;
        }
    }

    /// <summary>The extension follows the real format (the model may answer JPEG or WebP); a chosen name is kept otherwise.</summary>
    private static string WithExtensionFor(string path, string mediaType)
    {
        var extension = mediaType switch
        {
            ImageFormats.Jpeg => ".jpg",
            ImageFormats.WebP => ".webp",
            ImageFormats.Gif => ".gif",
            _ => ".png",
        };
        var current = Path.GetExtension(path);
        var matches = current.Equals(extension, StringComparison.OrdinalIgnoreCase)
            || (extension == ".jpg" && current.Equals(".jpeg", StringComparison.OrdinalIgnoreCase));
        return matches ? path : Path.ChangeExtension(path, extension);
    }
}
