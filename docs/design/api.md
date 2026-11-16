# API contract

> **Status:** Step 08 (model selection) implemented on 2026-11-16, on top of steps 04, 06 and 07 (chat endpoint, images, streaming). The web app mirrors these contracts in `frontend/src/services/types.ts`; keep both in sync.

`DotNextAI.Api` is an ASP.NET Core 10 Minimal API on `http://localhost:5100`. It exposes the same chat service as the CLI ([chat service](../specs/feature-llm-chat.md)) and is meant for local use by the web app. In Development the OpenAPI document is at `/openapi/v1.json` and Swagger UI at `/swagger`; `backend/src/DotNextAI.Api/DotNextAI.Api.http` has ready-made requests.

## Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| State | Stateless. The client keeps the conversation and sends its history with every question, as the CLI does. No sessions, no storage. | 2026-11-16 |
| Errors | `ProblemDetails` (`application/problem+json`). Validation errors are a 400 `ValidationProblem` with `errors` per field. Model failures carry a `failure` extension with the `ChatFailure` name, so the multi-language web app maps it to its own text; `detail` is English and only for logs and developers. | 2026-11-16 |
| Status codes | Problems on the server side or in the proxy are 5xx: 502 when the proxy rejects or fails, 503 when the model is unavailable or not configured, 504 on timeout. 429 for rate limits, either the proxy's or the API's own. 422 when the proxy refuses the content, e.g. context window exceeded. 401/403 from the proxy are **not** forwarded: the key belongs to the server, not the caller. | 2026-11-16 |
| Rate limit | `POST /api/chat` has a fixed window per client IP: `RateLimiting:ChatPermitsPerMinute`, default 20, range 1-10,000. The API has no authentication and every call spends the LiteLLM key, so this caps a runaway script or an exposed port. | 2026-11-16 |
| Streaming (step 07) | `POST /api/chat/stream` takes the same body and rules as `/api/chat` and answers with Server-Sent Events (`TypedResults.ServerSentEvents`). The first piece is awaited **before** the stream starts, so the usual failures keep their ProblemDetails and status code; only a failure after text was sent travels as an `error` event with the same `failure` code. | 2026-11-16 |
| Models (step 08) | `GET /api/models` lists the proxy's chat models (`mode = chat`), the default first. The chat endpoints take an optional `model`; anything other than the default must be in that list, so a client cannot send arbitrary model names to the proxy (400 `errors.model`). | 2026-11-16 |
| JSON | camelCase properties; enums as camelCase strings (`"user"`, `"assistant"`), integers rejected. | 2026-11-16 |
| Contracts | HTTP DTOs live in `DotNextAI.Api/Contracts` (`ChatRequestDto`, `ChatTurnDto`, `ChatResponseDto`) and map to the Application types; `HealthStatusDto` is in Application. | 2026-11-16 |

## Endpoints

### `GET /api/health`

`200`:

```json
{ "status": "Healthy", "service": "DotNextAI.Api", "environment": "Development", "llmConfigured": true, "timestampUtc": "2026-11-16T16:42:34.33+00:00" }
```

`llmConfigured` is true when endpoint, key and default model are set. The answer never contains the endpoint or the key.

### `GET /api/models`

`200`:

```json
{
  "defaultModel": "gpt-5.6-luna",
  "models": [
    { "id": "gpt-5.6-luna", "isDefault": true, "maxInputTokens": 922000, "maxOutputTokens": 128000 },
    { "id": "gpt-5.6-terra", "isDefault": false, "maxInputTokens": 922000, "maxOutputTokens": 128000 }
  ]
}
```

Read from the proxy's `GET {Llm:Endpoint}models`, which is filtered by the key's access (unlike LiteLLM's `/v2/model/info`), and cached for 5 minutes (failures are not cached). Entries whose `mode` is not `chat` (embeddings, image generation, realtime) are left out; an entry without `mode` (a plain OpenAI-compatible list) counts as chat. The default is always included, first. Errors: 503 `NotConfigured`, or the proxy's failure (`Unauthorized` 502, `Unavailable` 502, `Timeout` 504…).

### `POST /api/chat`

Asks a question after the previous turns of a conversation.

Request:

```json
{
  "question": "What is my name?",
  "history": [
    { "role": "user", "text": "My name is Ana." },
    { "role": "assistant", "text": "Nice to meet you, Ana." }
  ],
  "images": [
    { "mediaType": "image/png", "data": "iVBORw0KGgoAAAANSUhEUgAAAAEAAAAB..." }
  ]
}
```

| Field | Rules |
| ----- | ----- |
| `question` | Required. Trimmed; 1-20,000 characters. |
| `history` | Optional (omitted or `[]` for a first question). Oldest first, at most 100 turns (50 questions and their answers). Every turn needs `role` (`"user"` or `"assistant"`; `"system"` and numbers are rejected) and a non-empty `text`. |
| `model` | Optional (step 08). An `id` from `GET /api/models`; omitted (or the default) uses `Llm:DefaultModel` without reading the list. Another model is checked against the list: 400 `errors.model` if it is not a chat model, or the list's failure if it cannot be read. |
| `images` | Optional (step 06). Images for **this question only**; the history stays text. At most 4. `mediaType` is `image/png`, `image/jpeg`, `image/webp` or `image/gif` and must match the bytes; `data` is the file in base64 (no `data:` prefix), at most 4 MB decoded. Oversized data is rejected before it is decoded. |

`200`:

```json
{ "answer": "Your name is Ana.", "model": "gpt-5.6-luna", "truncated": false }
```

`truncated` is true when the answer stopped at the maximum output length. To continue the conversation, the client appends `{ role: "user", text: question }` and `{ role: "assistant", text: answer }` to its history. The question goes as sent; the server trims it.

### `POST /api/chat/stream`

Same request body, validation, rate limit and failures before the answer as `POST /api/chat`. On success, `200` with `Content-Type: text/event-stream`:

```text
event: delta
data: {"text":"Your name "}

event: delta
data: {"text":"is Ana."}

event: done
data: {"answer":"Your name is Ana.","model":"gpt-5.6-luna","truncated":false}
```

| Event | Data | When |
| ----- | ---- | ---- |
| `delta` | `{ text }` | Each piece of the answer, in order. Their concatenation is the answer. |
| `done` | `{ answer, model, truncated }` | Last event of a complete answer; same shape as the `POST /api/chat` response. |
| `error` | `{ status, failure, title, detail }` | Last event when the answer breaks after it started (e.g. `Unavailable` when the connection to the proxy drops); the same `failure` codes and statuses as the problems below. |

A stream that ends with neither `done` nor `error` (the connection closed) is a failure for the client. Closing the request stops the model call.

## Errors

| Status | `failure` | When |
| ------ | --------- | ---- |
| 400 | — (`errors.question` / `errors.history` / `errors.images` / `errors.model`) | Invalid question, history or images (count, base64, size, format, declared type different from the content), unknown role, malformed JSON |
| 405 | — | Any method other than `POST` on `/api/chat` |
| 422 | `BadRequest` | The proxy refused the request, e.g. context window exceeded |
| 429 | `RateLimited` | The API's own limit (with a `Retry-After` header in seconds), or the proxy's rate limit or budget |
| 502 | `Unauthorized` | The proxy rejected the server's key |
| 502 | `ModelNotFound` | The proxy does not know the configured model |
| 502 | `Unavailable` | Proxy unreachable or a 5xx after retries |
| 502 | `EmptyAnswer` | The model returned no text |
| 503 | `NotConfigured` | Endpoint, key or default model missing on the server |
| 503 | `ModelUnavailable` | The proxy has no healthy deployment for the model |
| 504 | `Timeout` | No answer within `Llm:TimeoutSeconds` |

Example:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "The model is unavailable",
  "status": 503,
  "detail": "The model 'gpt-5.6-luna' is currently unavailable in the LiteLLM proxy (no healthy deployments).",
  "failure": "ModelUnavailable",
  "traceId": "00-56c0af94…-00"
}
```

`detail` follows the [chat service failures](../specs/feature-llm-chat.md#failures). It never contains the key; for 400/404/422 from the proxy it may quote the first line of the proxy's message.

## CORS

Only the origins in `Cors:AllowedOrigins` (`http://localhost:3030` in Development), methods `GET` and `POST`, any header.

## Security notes

- No authentication: the API is for local use (`launchSettings.json` binds to `localhost`). The rate limit reduces the damage of misuse; an exposed deployment needs authentication first (see [todo.md](../tasks/todo.md)).
- The proxy key and endpoint never appear in answers or problems.
- Prompts and answers are not logged.

## Verification

- `ChatEndpointTests` (integration, `WebApplicationFactory` with a fake `IChatService`):
  - Answers: answer with model and `truncated`; history passed in order; trimmed question.
  - Images: decoded and passed to the model; 400 for invalid base64, a type mismatch, a non-image, missing fields, too many and too large (rejected before decoding).
  - Validation: 400 for an empty, missing or too-long question; empty, role-less or null turns; history over the limit; `"system"` or numeric roles; malformed JSON. None of them calls the model.
  - Failures: 503 when not configured; the status, `failure`, `title` and `detail` of every `ChatFailure`.
  - Limits and CORS: 429 with `Retry-After` over the rate limit, the CORS preflight for `POST`, 405 for `GET`.
- `ModelsEndpointTests` (integration):
  - `GET /api/models`: the list and its limits; 503 when not configured; the proxy's failure.
  - `model` on both chat endpoints: a chat model passes; the default reads no list; a non-chat or unknown model gets 400; a failure to read the list.
- `ChatStreamEndpointTests` (integration):
  - Events: `delta`s then `done`, `text/event-stream`, the same input as `/api/chat`.
  - Errors: 400 and 503 as ProblemDetails, not a stream; failures before the first piece with their status and `failure`; a failure mid-answer as an `error` event.
  - The shared rate limit.
- `HealthEndpointTests`: health answer, no secrets, CORS.
- Manual: `DotNextAI.Api.http`, or `curl` against `./scripts/dev.sh`.
