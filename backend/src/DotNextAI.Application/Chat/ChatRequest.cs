namespace DotNextAI.Application.Chat;

/// <summary>One question to the model, with everything that goes with it.</summary>
/// <param name="Question">Non-empty question of at most <see cref="ChatRules.MaxQuestionLength"/> characters.</param>
/// <param name="History">Previous turns, oldest first (text only); empty for a single question.</param>
/// <param name="Images">Images attached to this question (step 06); empty when there are none.</param>
/// <param name="Model">
/// Chat model to answer (step 08): one of <see cref="IModelCatalog.GetChatModelsAsync"/>, or null for
/// <c>Llm:DefaultModel</c>.
/// </param>
public sealed record ChatRequest(
    string Question,
    IReadOnlyList<ChatTurn> History,
    IReadOnlyList<ChatImage> Images,
    string? Model = null);
