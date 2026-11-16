# LLM chat service

> **Status:** Step 06 (image input) implemented on 2026-11-16, on top of steps 02-03 (Microsoft Agent Framework agent, conversation history).

How DotNextAI talks to the language model. The contract lives in Application (`DotNextAI.Application/Chat`) and the implementation in Infrastructure (`DotNextAI.Infrastructure/Chat`). Every front end uses it: the CLI and the API (`POST /api/chat`, [API contract](../design/api.md)).

## Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| Contract | `IChatService` with `IsConfigured` and `AskAsync(question, history)` returning `ChatAnswer(Text, Model, Truncated)`. The extension `AskAsync(question)` sends no history. Until step 03 there was only `AskAsync(question)`. Failures are `ChatServiceException` with a typed `ChatFailure`, so each front end chooses its own message or HTTP status. | 2026-11-16 |
| Step 01 implementation | `OpenAiChatService`: OpenAI .NET SDK (`ChatClient`, `CompleteChatAsync`) pointed at the LiteLLM proxy, with no orchestration layer. This is the course's "direct OpenAI API calls (through LiteLLM)". **Replaced by step 02**; the code is in git history (commit `2fcbc2a`). | 2026-11-16 |
| Step 02 implementation | `AgentChatService`: the same OpenAI SDK client turned into a Microsoft Agent Framework `ChatClientAgent` (`ChatClient.AsAIAgent`, package `Microsoft.Agents.AI.OpenAI` 1.28.0), one `RunAsync` per question. This is the course's "call the LLM through LangChain" step, with Agent Framework as the .NET orchestration framework. The direct implementation is not kept next to it: one code path, and the agent is what steps 03-10 build on. | 2026-11-16 |
| Shared pieces | `LlmChatClientFactory` (OpenAI client: endpoint, key, timeout, retries, transport) and `ChatFailureMapper` (exceptions to `ChatFailure`) are separate from the agent, so later clients (model list, image generation) reuse them. | 2026-11-16 |
| Transport | The SDK's HTTP pipeline runs on an `HttpClient` from `IHttpClientFactory` (named client `Llm`), so tests stub the HTTP layer and never reach the proxy. `HttpClient.Timeout` is infinite; the SDK's `NetworkTimeout` (`Llm:TimeoutSeconds`) applies per attempt instead. | 2026-11-16 |
| Retries | The SDK's retry policy with `Llm:MaxRetries` (default 2, range 0-5): exponential back-off on 408, 429, 5xx and network errors. | 2026-11-16 |
| Endpoint | `Llm:Endpoint` is the base URL, used as is; the SDK appends `chat/completions`. The ERNI proxy serves it with and without `/v1`. | 2026-11-16 |
| Error details | For 400, 404 and 422 the message quotes the first line of the proxy's `error.message` (max 300 characters, the API key masked). For 401/403 the body is never quoted, because LiteLLM may echo the key it rejected. | 2026-11-16 |
| "No healthy deployments" | A LiteLLM 400 whose message says the model group has no healthy deployments is its own failure, `ModelUnavailable`, not `BadRequest`: the request is fine, the model is down. Seen with every chat model of the ERNI proxy on 2026-11-16. | 2026-11-16 |
| Prompt | Step 01: one user message with the question. Step 02: the agent's instructions as a system message, then the question ([agent](#agent)). | 2026-11-16 |
| History owner (step 03) | The front end keeps the history and sends it with every question; the service is stateless and uses no `AgentSession`. `ChatConversation` (Application) does the bookkeeping for the CLI, and the web app will keep its own in the browser. | 2026-11-16 |
| Why not `AgentSession` | A server-side session would make the API (step 04) stateful: per-user storage, expiry, and affinity across instances. Sending the history matches the rule "conversations live in memory, in the CLI process or the browser" and keeps every request self-contained. | 2026-11-16 |
| Images (step 06) | `AskAsync(question, history, images)`: each image is a `ChatImage(MediaType, Data)` sent as a `DataContent` next to the question text; the SDK turns it into an OpenAI `image_url` part with a base64 data URI. The model (`gpt-5.6-luna`) is assumed to accept images; this is to be confirmed against the real proxy. | 2026-11-16 |
| Images only with their question | An image travels with the question it was attached to; the history stays text. Later questions see the model's earlier answer about the image, not the image. Resending every image with every turn would make each request up to tens of MB and multiply the image tokens. | 2026-11-16 |
| Format from the bytes | `ImageFormats.Detect` reads the file signature (PNG, JPEG, GIF, WebP). A declared media type that does not match the bytes is rejected; file names and extensions are never trusted. | 2026-11-16 |
| Package line | Agent Framework 1.28.0 depends on `Microsoft.Extensions.AI` 10.11.0 and `OpenAI` ≥ 2.14, so it stays on the .NET 10 line (no 11.x package in any lock file). | 2026-11-16 |

## Rules

- The question is trimmed. It must not be empty or longer than `ChatRules.MaxQuestionLength` = **20,000 characters**. That limit keeps an accidental paste or pipe of a large file from being sent (and billed) as one prompt. Invalid questions never reach the proxy (`ArgumentException` from `AskAsync`; front ends validate first with `ChatRules.TryNormalizeQuestion`).
- Without endpoint, key and default model, `IsConfigured` is false and `AskAsync` throws `NotConfigured` without any HTTP call. The API still starts.
- The answer is the agent response's text (`AgentResponse.Text`, all text contents). If there is no text but a refusal (read from the underlying OpenAI completion), the refusal is the answer. If there is neither, the call fails with `EmptyAnswer`.
- `Truncated` is true when the finish reason is `length`.
- `Model` is the model reported by the proxy (`ChatResponse.ModelId` under the agent response), or the configured default when the proxy reports none.
- Logs hold metadata only: model, finish reason, duration, HTTP status. Never the prompt, the answer, the key or the proxy's error body.
- Caller cancellation propagates as `OperationCanceledException`. A timeout without caller cancellation becomes `Timeout`.

## Agent

| Property | Value |
| -------- | ----- |
| Type | `ChatClientAgent` over the OpenAI SDK `ChatClient` of the default model |
| Name | `DotNextAI` |
| Instructions | `You are DotNextAI, a helpful assistant. Answer clearly and concisely, in the language of the question.` (`AgentChatService.Instructions`) |
| Session | None: each `AskAsync` is a new run. The history comes as messages from the caller ([conversation history](#conversation-history)). |
| Tools | None |

The HTTP request is the same as step 01 plus the system message and the history: `POST {endpoint}chat/completions` with `[system: instructions, …history (user/assistant), user: question]`. Errors still come from the OpenAI SDK (`ClientResultException`) through the agent and are mapped as below.

## Conversation history

| Piece | Behavior |
| ----- | -------- |
| `ChatTurn(Role, Text)` | One message of the history: `User` or `Assistant`. |
| `IChatService.AskAsync(question, history)` | Sends the history in order, oldest first, as user and assistant messages between the instructions and the new question. |
| `ChatRules.ValidateHistory` | At most `MaxHistoryTurns` = 100 turns (50 questions and their answers), none empty; otherwise `ArgumentException` and no HTTP call. |
| `ChatConversation` | Keeps the turns of one conversation. `AskAsync` sends a snapshot of the history and records the question and the answer **only on success**. `IsFull` at `MaxConversationQuestions` = 50; asking then throws `InvalidOperationException`. `Reset` forgets everything. |

Every question resends the whole history, so tokens and latency grow with the conversation. The 50-question limit bounds that; the models' context (922,000 input tokens) is not the constraint.

## Images

| Rule | Value |
| ---- | ----- |
| Formats | PNG, JPEG, WebP, GIF (`ImageFormats.Supported`), detected from the bytes |
| Per question | at most `ChatRules.MaxImagesPerQuestion` = 4 |
| Size | at most `ChatRules.MaxImageBytes` = 4 MB each, before base64 |
| Question text | still required (1-20,000 characters) |
| Validation | `ChatRules.TryValidateImages`: count, empty, size, unsupported content, declared type different from the content. `AskAsync` throws `ArgumentException` and sends nothing when it fails. |
| Request to the proxy | `[system: instructions, …history (text), user: [text: question, image_url: data:<type>;base64,…, …]]` |
| `ChatConversation` | `AskAsync(question, images)` sends the images once and records the question text only. |

## Failures

| `ChatFailure` | When | Message (shown to the user) |
| ------------- | ---- | --------------------------- |
| `NotConfigured` | Endpoint, key or default model missing | `The LiteLLM proxy is not configured (Llm:Endpoint, Llm:ApiKey and Llm:DefaultModel).` |
| `Unauthorized` | 401, 403 | `The LiteLLM proxy rejected the API key.` |
| `ModelNotFound` | 404 | `The LiteLLM proxy does not know the model '<model>'` + `: <proxy detail>` or `.` |
| `ModelUnavailable` | 400 with "no healthy deployments" | `The model '<model>' is currently unavailable in the LiteLLM proxy (no healthy deployments).` |
| `BadRequest` | 400, 413, 422 | `The LiteLLM proxy rejected the request` + `: <proxy detail>` or `.` |
| `RateLimited` | 429 after the retries | `The rate limit or budget of the API key was reached. Try again later.` |
| `Timeout` | No answer within `Llm:TimeoutSeconds` | `The model did not answer within <n> seconds.` |
| `Unavailable` | Network error, 5xx after the retries, other statuses | `The LiteLLM proxy could not be reached.` or `The LiteLLM proxy answered with HTTP <status>.` |
| `EmptyAnswer` | Completion without text or refusal | `The model returned an empty answer.` |

## Configuration

| Setting | Default | Range | Meaning |
| ------- | ------- | ----- | ------- |
| `Llm:Endpoint` | — | HTTPS, or HTTP on loopback | Proxy base URL |
| `Llm:ApiKey` | — | — | Proxy virtual key (secret, user-secrets) |
| `Llm:DefaultModel` | — | — | Chat model, e.g. `gpt-5.6-luna` ([available models](product-rules.md#models-available-through-the-proxy)) |
| `Llm:TimeoutSeconds` | 120 | 5-600 | Timeout of each attempt |
| `Llm:MaxRetries` | 2 | 0-5 | SDK retries |

## Verification

- `AgentChatServiceTests` (unit, stub `HttpMessageHandler`), the step 01 suite kept and extended for the agent:
  - Agent: instructions sent as the system message before the question; without history, each question goes alone; with history, the turns go in order; a history over the limit is rejected without HTTP; model reported by the proxy.
- `ChatConversationTests`: turns recorded on success only, snapshot of the history, limit, reset, images sent once with the text kept in the history.
- `ImageRulesTests`: signature detection of the four formats, and every image rule.
- `AgentChatServiceTests` (images): base64 data URIs after the question text, history kept as text, an invalid image rejected without HTTP.
  - Request shape: `POST {endpoint}chat/completions`, bearer key, model, one user message, trimmed question; an endpoint with a `/v1/` path.
  - Answers: answer and model; `Truncated` on `length`; refusal as answer; `EmptyAnswer`.
  - Failure mapping per status code, including the real "no healthy deployments" body.
  - Error details: first-line detail, key masking, no body quoted on 401, non-JSON bodies.
  - Network error, timeout, caller cancellation, `NotConfigured` and empty questions, the last two without HTTP calls.
- `ChatRulesTests`: trimming, the 20,000-character limit and the history rules.
- `LlmOptionsTests`: endpoint, key and model rules of `IsConfigured`.
