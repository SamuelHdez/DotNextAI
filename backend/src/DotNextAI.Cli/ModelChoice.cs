using System.Globalization;
using DotNextAI.Application.Chat;

namespace DotNextAI.Cli;

/// <summary>Model selection in the terminal app (backlog step 08).</summary>
internal static class ModelChoice
{
    /// <summary>
    /// Checks a model asked for with --model or /model against the proxy's chat models. Writes the reason on stderr and
    /// returns false when it is not one of them; a failure to read the list propagates.
    /// </summary>
    public static async Task<bool> IsChatModelAsync(IModelCatalog catalog, string model, TextWriter error, CancellationToken cancellationToken)
    {
        if (string.Equals(model, catalog.DefaultModel, StringComparison.Ordinal))
        {
            return true;
        }

        var models = await catalog.GetChatModelsAsync(cancellationToken);
        if (models.Any(chatModel => string.Equals(chatModel.Id, model, StringComparison.Ordinal)))
        {
            return true;
        }

        await error.WriteLineAsync($"'{model}' is not a chat model of the LiteLLM proxy. Available: {string.Join(", ", models.Select(m => m.Id))}.");
        return false;
    }

    /// <summary>One line per model: "* id (default, 922,000 input / 128,000 output tokens)".</summary>
    public static IEnumerable<string> Describe(IReadOnlyList<ChatModel> models, string? current = null)
    {
        var width = models.Max(model => model.Id.Length);
        foreach (var model in models)
        {
            var details = new List<string>();
            if (model.IsDefault)
            {
                details.Add("default");
            }

            if (model.MaxInputTokens is { } input && model.MaxOutputTokens is { } output)
            {
                details.Add(string.Create(CultureInfo.InvariantCulture, $"{input:N0} input / {output:N0} output tokens"));
            }

            var marker = string.Equals(model.Id, current ?? models.FirstOrDefault(m => m.IsDefault)?.Id, StringComparison.Ordinal) ? "*" : " ";
            yield return details.Count == 0 ? $"{marker} {model.Id}" : $"{marker} {model.Id.PadRight(width)}  ({string.Join(", ", details)})";
        }
    }
}
