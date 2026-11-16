# Token metrics

> **Status:** Step 10 implemented on 2026-11-16 in the web app, the CLI and the API. With it, every step of the course backlog is done.

The course asks for the *consumed tokens per call and in total for the conversation*. DotNextAI shows what the proxy reports for every chat answer and every generated image. A conversation's total is what it cost, call by call.

## Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| Source | The provider's own counters, as the proxy relays them: `usage` of chat completions (`prompt_tokens`, `completion_tokens`, `total_tokens`), surfaced by Agent Framework as `AgentResponse.Usage` / `UsageContent`, and `usage` of the images API (`input_tokens`, `output_tokens`, `total_tokens`). Nothing is estimated locally. | 2026-11-16 |
| Streaming | The OpenAI SDK already sends `stream_options: { include_usage: true }` (checked in a test). The usage arrives in a final chunk without choices and is attached to the complete answer. A stopped answer has no usage: it never reached that chunk. | 2026-11-16 |
| Shape | `TokenUsage(InputTokens, OutputTokens, TotalTokens)` on `ChatAnswer` and `GeneratedImage`. It is null when the proxy reports nothing, so a provider without usage shows no misleading zeros. A missing total is input + output. | 2026-11-16 |
| Conversation total | The **sum of the calls**. Every question resends the whole history, so input tokens grow with each turn, and the total reflects what was really consumed, not the length of the visible text. The UI says so in a hint. | 2026-11-16 |
| Where | Web: each answer, the conversation total above the composer, and each generated image. CLI: a `Tokens:` line on stderr after each answer or image (only when known, so stdout and quiet providers stay clean), plus `/usage` in the chat. API: `usage` on `/api/chat`, the `done` event of `/api/chat/stream` and `/api/images`. | 2026-11-16 |
| No cost | Token counts only. Prices change per model and deal, and the proxy (LiteLLM) is the place that tracks spend. | 2026-11-16 |

## Formats

| Where | Text |
| ----- | ---- |
| Web, each answer | *"1,200 in · 34 out tokens"* next to the model (Spanish: *"1200 de entrada · 34 de salida"*) |
| Web, conversation | *"This conversation: 2,600 tokens (2,500 in · 100 out) over 2 answers."*, followed by *"Every question resends the conversation, so input tokens grow with each turn."* Updated after every answer, and reset by *New conversation*. |
| Web, image | *"4,172 tokens (12 in · 4,160 out)"* |
| CLI, `ask` and `image` | `Tokens: 1,200 in · 34 out · 1,234 total` (stderr) |
| CLI, chat | `Tokens: 1,200 in · 34 out · 1,234 total (conversation: 2,468)` (stderr) |
| CLI, `/usage` | `This conversation: 2 answers, 2,400 in · 68 out · 2,468 total tokens. …`, or `No questions answered yet.`, or `N answers; the LiteLLM proxy did not report their tokens.` |
| API | `"usage": { "inputTokens": 1200, "outputTokens": 34, "totalTokens": 1234 }` or `null` |

Numbers follow the UI language in the web app (`Intl.NumberFormat`) and the invariant culture in the CLI.

## Verification

- .NET:
  - `TokenUsageTests`: counters, a missing total, nothing reported, sums; the conversation total (unknown usage adds nothing; streamed answers count, failures do not; reset).
  - Service counters: `AgentChatServiceTests` (usage of a completion, none reported); `AgentChatServiceStreamingTests` (usage from the final chunk, none without it); `LiteLlmImageServiceTests` (usage of a generation, none without it).
  - Front ends: `UsageCommandTests` (CLI: `ask`, the chat with totals, `/usage`, no line without usage, `image`); `UsageEndpointTests` (`usage` on `/api/chat`, the `done` event and `/api/images`, null without it).
- Web: `ChatViewUsage.test.tsx` (each answer and the total, Spanish numbers, nothing without usage, reset with a new conversation, `conversationUsage`) and `ImagesView.test.tsx` (image tokens).
- E2E: `chat.spec.ts` (a token caption per answer and a total that more than doubles on the second question, because the history is resent) and `images.spec.ts` (image tokens).
