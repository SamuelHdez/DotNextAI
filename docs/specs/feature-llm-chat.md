# LLM chat service

> **Status:** Step 01 (direct single-question call) implemented on 2026-11-16. Step 02 moves the call to Microsoft Agent Framework behind the same contract.

How DotNextAI talks to the language model. The contract lives in Application (`DotNextAI.Application/Chat`) and the implementation in Infrastructure (`DotNextAI.Infrastructure/Chat`). Every front end uses it: the CLI today, the API from step 04.

## Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| Contract | `IChatService` with `IsConfigured` and `AskAsync(question)` returning `ChatAnswer(Text, Model, Truncated)`. Failures are `ChatServiceException` with a typed `ChatFailure`, so each front end chooses its own message or HTTP status. | 2026-11-16 |
| Step 01 implementation | `OpenAiChatService`: OpenAI .NET SDK (`ChatClient`, `CompleteChatAsync`) pointed at the LiteLLM proxy, with no orchestration layer. This is the course's "direct OpenAI API calls (through LiteLLM)". | 2026-11-16 |
| Transport | The SDK's HTTP pipeline runs on an `HttpClient` from `IHttpClientFactory` (named client `Llm`), so tests stub the HTTP layer and never reach the proxy. `HttpClient.Timeout` is infinite; the SDK's `NetworkTimeout` (`Llm:TimeoutSeconds`) applies per attempt instead. | 2026-11-16 |
| Retries | The SDK's retry policy with `Llm:MaxRetries` (default 2, range 0-5): exponential back-off on 408, 429, 5xx and network errors. | 2026-11-16 |
| Endpoint | `Llm:Endpoint` is the base URL, used as is; the SDK appends `chat/completions`. The ERNI proxy serves it with and without `/v1`. | 2026-11-16 |
| Error details | For 400, 404 and 422 the message quotes the first line of the proxy's `error.message` (max 300 characters, the API key masked). For 401/403 the body is never quoted, because LiteLLM may echo the key it rejected. | 2026-11-16 |
| "No healthy deployments" | A LiteLLM 400 whose message says the model group has no healthy deployments is its own failure, `ModelUnavailable`, not `BadRequest`: the request is fine, the model is down. Seen with every chat model of the ERNI proxy on 2026-11-16. | 2026-11-16 |
| Prompt | One user message with the question. No system prompt in step 01. | 2026-11-16 |

## Rules

- The question is trimmed. It must not be empty or longer than `ChatRules.MaxQuestionLength` = **20,000 characters**. That limit keeps an accidental paste or pipe of a large file from being sent (and billed) as one prompt. Invalid questions never reach the proxy (`ArgumentException` from `AskAsync`; front ends validate first with `ChatRules.TryNormalizeQuestion`).
- Without endpoint, key and default model, `IsConfigured` is false and `AskAsync` throws `NotConfigured` without any HTTP call. The API still starts.
- The answer is the concatenated text parts of the completion. If there is no text but a refusal, the refusal is the answer. If there is neither, the call fails with `EmptyAnswer`.
- `Truncated` is true when the finish reason is `length`.
- `Model` is the model reported by the proxy, or the configured default when the proxy reports none.
- Logs hold metadata only: model, finish reason, duration, HTTP status. Never the prompt, the answer, the key or the proxy's error body.
- Caller cancellation propagates as `OperationCanceledException`. A timeout without caller cancellation becomes `Timeout`.

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

- `OpenAiChatServiceTests` (unit, stub `HttpMessageHandler`):
  - Request shape: `POST {endpoint}chat/completions`, bearer key, model, one user message, trimmed question; an endpoint with a `/v1/` path.
  - Answers: answer and model; `Truncated` on `length`; refusal as answer; `EmptyAnswer`.
  - Failure mapping per status code, including the real "no healthy deployments" body.
  - Error details: first-line detail, key masking, no body quoted on 401, non-JSON bodies.
  - Network error, timeout, caller cancellation, `NotConfigured` and empty questions, the last two without HTTP calls.
- `ChatRulesTests`: trimming and the 20,000-character limit.
- `LlmOptionsTests`: endpoint, key and model rules of `IsConfigured`.
