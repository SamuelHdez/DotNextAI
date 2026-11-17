# Token metrics

> **Status:** Step 10 implemented on 2026-11-16 in the web app, the CLI and the API. With it, every step of the course backlog is done. Cost of every chat answer and of the conversation added on 2026-11-17.

The course asks for the *consumed tokens per call and in total for the conversation*. DotNextAI shows what the proxy reports for every chat answer and every generated image, and, since 2026-11-17, what each chat answer and the whole conversation cost in US dollars at the proxy's prices. A conversation's total is what it cost, call by call.

## Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| Source | The provider's own counters, as the proxy relays them: `usage` of chat completions (`prompt_tokens`, `completion_tokens`, `total_tokens`), surfaced by Agent Framework as `AgentResponse.Usage` / `UsageContent`, and `usage` of the images API (`input_tokens`, `output_tokens`, `total_tokens`). Nothing is estimated locally. | 2026-11-16 |
| Streaming | The OpenAI SDK already sends `stream_options: { include_usage: true }` (checked in a test). The usage arrives in a final chunk without choices and is attached to the complete answer. A stopped answer has no usage: it never reached that chunk. | 2026-11-16 |
| Shape | `TokenUsage(InputTokens, OutputTokens, TotalTokens, Cost)` on `ChatAnswer` and `GeneratedImage` (`Cost` since 2026-11-17). It is null when the proxy reports nothing, so a provider without usage shows no misleading zeros. A missing total is input + output. | 2026-11-16 |
| Conversation total | The **sum of the calls**. Every question resends the whole history, so input tokens grow with each turn, and the total reflects what was really consumed, not the length of the visible text. The UI says so in a hint. | 2026-11-16 |
| Where | Web: each answer, the conversation total above the composer, and each generated image. CLI: a `Tokens:` line on stderr after each answer or image (only when known, so stdout and quiet providers stay clean), plus `/usage` in the chat. API: `usage` on `/api/chat`, the `done` event of `/api/chat/stream` and `/api/images`. | 2026-11-16 |
| Cost | **Shown since 2026-11-17** for chat answers and conversations. This replaced "token counts only, no cost" (2026-11-16): the user wants to see what a conversation costs, and the proxy already holds the prices, so they are read from it instead of being copied into the code. | 2026-11-17 |
| Price source | LiteLLM's `GET {Llm:Endpoint}model/info` (`model_info` of each `model_name`, US dollars per token), the same prices the proxy uses to track spend. Read through `LiteLlmHttp`, cached for 5 minutes (`LiteLlmModelPrices`). If it fails, the answer still arrives without a cost, and the next try waits 1 minute so a proxy without the route does not slow every answer. A model whose input and output prices are both missing or 0 has no price. Prices of the ERNI proxy on 2026-11-17: [product rules](product-rules.md#models-available-through-the-proxy). | 2026-11-17 |
| Cost of a call | Priced with the model that was asked for (the proxy's `model_name`), as LiteLLM does: `(input − cached) × input price + cached × cache-read price + output × output price`. Cached input tokens come from `UsageDetails.CachedInputTokenCount` (OpenAI's `prompt_tokens_details.cached_tokens`), 0 when not reported; without a cache-read price they cost the input price. When the input is above a tier of the model (`input_cost_per_token_above_272k_tokens` and its `output_` and `cache_read_` siblings), the whole call uses that tier's prices. Rounded to 10 decimals. `_flex` (service tier) prices are not used. | 2026-11-17 |
| Unknown cost | `TokenUsage.Cost` is null when the call has no usage or its model no price. A conversation's cost is the sum of its calls' costs, and it becomes unknown (null) as soon as one call with usage has no cost, so a partial sum never passes for the total. Saved chats from before 2026-11-17 have no cost. A stopped answer has no usage, so it adds neither tokens nor cost, as before. | 2026-11-17 |
| Kept with the chat | The cost of each saved answer and the conversation's running total are stored when the exchange is saved (`ConversationCost` migration), so a later price change does not rewrite old chats. | 2026-11-17 |
| Images | Generated images show tokens only: `gpt-image-2` is priced per image token, which the images API reports apart. A follow-up. | 2026-11-17 |

## Formats

| Where | Text |
| ----- | ---- |
| Web, each answer | *"1,200 in · 34 out tokens"* and *"$0.00014"* next to the model (Spanish: *"1200 de entrada · 34 de salida"*, *"0,00014 US$"*) |
| Web, conversation | *"This conversation: 2,600 tokens (2,500 in · 100 out) over 2 answers."*, then *"Cost: $0.0003, at the LiteLLM proxy's prices."* when known, then *"Every question resends the conversation, so input tokens grow with each turn."* Updated after every answer, and reset by *New conversation*. |
| Web, image | *"4,172 tokens (12 in · 4,160 out)"* |
| CLI, `ask` and `image` | `Tokens: 1,200 in · 34 out · 1,234 total · $0.00014` (stderr; no cost part when unknown) |
| CLI, chat | `Tokens: 1,200 in · 34 out · 1,234 total · $0.00014 (conversation: 2,468 · $0.0003)` (stderr) |
| CLI, `/usage` | `This conversation: 2 answers, 2,400 in · 68 out · 2,468 total tokens, $0.0003 at the LiteLLM proxy's prices. …`, or `No questions answered yet.`, or `N answers; the LiteLLM proxy did not report their tokens.` |
| API | `"usage": { "inputTokens": 1200, "outputTokens": 34, "totalTokens": 1234, "cost": 0.000137 }` or `null`; `cost` is null when unknown |

Numbers follow the UI language in the web app (`Intl.NumberFormat`) and the invariant culture in the CLI. Costs are US dollars with two significant digits below $1 (`$0.00014`, `$0.012`, `$0.25`) and two decimals from $1 (`$1.23`), the same rule in the web (`formatCost`) and the CLI (`CliMessages.Cost`).

## Verification

- .NET:
  - `TokenUsageTests`: counters, a missing total, nothing reported, sums, costs summed and an unknown cost making the sum unknown; the conversation total (unknown usage adds nothing; streamed answers count, failures do not; reset).
  - Cost: `ModelPriceTests` (input, output, cached input with and without a cache-read price, the tier above 272k tokens, rounding); `LiteLlmModelPricesTests` (prices parsed from a `model/info` answer of the ERNI proxy, the route relative to the endpoint, the cache, failures not breaking and retried after a minute, models without prices); `AgentChatServiceTests` and `AgentChatServiceStreamingTests` (the cost of a completion and of a stream, priced with the model asked for, cached tokens, no cost without a price); `SqliteConversationStoreTests` (costs saved per answer and summed, unknown once a call has none).
  - Service counters: `AgentChatServiceTests` (usage of a completion, none reported); `AgentChatServiceStreamingTests` (usage from the final chunk, none without it); `LiteLlmImageServiceTests` (usage of a generation, none without it).
  - Front ends: `UsageCommandTests` (CLI: `ask`, the chat with totals, `/usage`, no line without usage, `image`); `UsageEndpointTests` (`usage` on `/api/chat`, the `done` event and `/api/images`, null without it).
- Web: `ChatViewUsage.test.tsx` (each answer and the total with their costs, Spanish numbers and currency, nothing without usage, no cost when unknown, reset with a new conversation, `conversationUsage`), `cost.test.ts` (`formatCost`) and `ImagesView.test.tsx` (image tokens).
- Manual, 2026-11-17, against the ERNI proxy: `dotnextai ask "Reply with exactly: OK"` with `gpt-6-luna` printed `Tokens: 39 in · 4 out · 43 total · $0.0000059`, which is 39 × $0.10 + 4 × $0.50 per million.
- E2E: `chat.spec.ts` (a token and cost caption per answer and a total that more than doubles on the second question, because the history is resent; the fake proxy serves `model/info` prices) and `images.spec.ts` (image tokens).
