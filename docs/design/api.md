# API contract

> **Status:** Saved chats (folders and conversations, part 1) implemented on 2026-11-16, on top of steps 04 and 06-10 (chat endpoint, image input, streaming, models, image generation, token metrics). The web app mirrors these contracts in `frontend/src/services/types.ts`; keep both in sync.

`DotNextAI.Api` is an ASP.NET Core 10 Minimal API on `http://localhost:5100`. It exposes the same chat service as the CLI ([chat service](../specs/feature-llm-chat.md)) and is meant for local use by the web app. In Development the OpenAPI document is at `/openapi/v1.json` and Swagger UI at `/swagger`; `backend/src/DotNextAI.Api/DotNextAI.Api.http` has ready-made requests.

## Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| State | `/api/chat` and `/api/chat/stream` stay stateless: the client keeps the conversation and sends its history with every question, as the CLI does. **Since 2026-11-16 (saved chats)** the `/api/conversations` endpoints keep conversations in a SQLite database and build the history on the server; they replace the old rule "no sessions, no storage" for the web app ([saved chats](../specs/feature-saved-chats.md)). | 2026-11-16 |
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
  "defaultModel": "gpt-6-luna",
  "models": [
    { "id": "gpt-6-luna", "isDefault": true, "maxInputTokens": 922000, "maxOutputTokens": 128000 },
    { "id": "gpt-6.1-terra", "isDefault": false, "maxInputTokens": 922000, "maxOutputTokens": 128000 }
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
{
  "answer": "Your name is Ana.",
  "model": "gpt-6-luna",
  "truncated": false,
  "usage": { "inputTokens": 1200, "outputTokens": 34, "totalTokens": 1234 }
}
```

`usage` (step 10, [token metrics](../specs/feature-token-metrics.md)) holds the tokens of this call as the proxy reports them, or `null` when it reports none. Input tokens include the history.

`truncated` is true when the answer stopped at the maximum output length. To continue the conversation, the client appends `{ role: "user", text: question }` and `{ role: "assistant", text: answer }` to its history. The question goes as sent; the server trims it.

### `POST /api/chat/stream`

Same request body, validation, rate limit and failures before the answer as `POST /api/chat`. On success, `200` with `Content-Type: text/event-stream`:

```text
event: delta
data: {"text":"Your name "}

event: delta
data: {"text":"is Ana."}

event: done
data: {"answer":"Your name is Ana.","model":"gpt-6-luna","truncated":false}
```

| Event | Data | When |
| ----- | ---- | ---- |
| `delta` | `{ text }` | Each piece of the answer, in order. Their concatenation is the answer. |
| `done` | `{ answer, model, truncated, usage }` | Last event of a complete answer; same shape as the `POST /api/chat` response, `usage` included. |
| `error` | `{ status, failure, title, detail }` | Last event when the answer breaks after it started (e.g. `Unavailable` when the connection to the proxy drops); the same `failure` codes and statuses as the problems below. |

A stream that ends with neither `done` nor `error` (the connection closed) is a failure for the client. Closing the request stops the model call.

### `POST /api/images`

Generates one image with the server's image model (step 09, [image generation](../specs/feature-images.md)).

```json
{ "prompt": "A fox reading by a lamp", "size": "landscape" }
```

| Field | Rules |
| ----- | ----- |
| `prompt` | Required. Trimmed; 1-4,000 characters (400 `errors.prompt`). |
| `size` | Optional: `"square"` (default, 1024×1024), `"landscape"` (1536×1024), `"portrait"` (1024×1536). Anything else is 400. |

`200`:

```json
{ "mediaType": "image/png", "data": "iVBORw0KGgo...", "model": "gpt-image-2", "revisedPrompt": "A watercolor fox…", "usage": { "inputTokens": 12, "outputTokens": 4160, "totalTokens": 4172 } }
```

`data` is the file in base64 (no `data:` prefix); `revisedPrompt` is null when the model reports none. Rate limit: `RateLimiting:ImagePermitsPerMinute` (default 5) per IP, separate from the chat's. Failures use the same `failure` codes and statuses as the chat; a refused prompt is 422 `BadRequest`, an answer without an image 502 `EmptyAnswer`.

## Saved chats

Folders and saved conversations of the single local user ([saved chats](../specs/feature-saved-chats.md)). No rate limit except on asking, which spends the key. Problems carry a `failure` extension with a `StoreFailure` name: `NotFound`, `FolderNotFound`, `DuplicateFolderName`, `ConversationFull`.

| Method | Path | Body | Success | Problems |
| ------ | ---- | ---- | ------- | -------- |
| `GET` | `/api/folders` | — | `200` `Folder[]` by name | — |
| `POST` | `/api/folders` | `{ name, icon?, color? }` | `201` `Folder` (icon `folder` and colour `default` when omitted) | 400 `errors.name` / `errors.icon` / `errors.color`; 409 `DuplicateFolderName` |
| `PATCH` | `/api/folders/{id}` | `{ name?, icon?, color? }`, at least one; omitted fields stay | `200` `Folder` | 400 `errors.name` (also when nothing is given) / `errors.icon` / `errors.color`; 404 `NotFound`; 409 `DuplicateFolderName` |
| `DELETE` | `/api/folders/{id}` | — | `204`; its conversations stay, with `folderId: null` | 404 `NotFound` |
| `GET` | `/api/conversations` | — | `200` `ConversationSummary[]`, last updated first | — |
| `POST` | `/api/conversations` | `{ title?, folderId? }` (both optional) | `201` `ConversationSummary`, title *New conversation* by default | 400 `errors.title`; 400 `FolderNotFound` |
| `GET` | `/api/conversations/{id}` | — | `200` `{ conversation, messages }` | 404 `NotFound` |
| `PATCH` | `/api/conversations/{id}` | `{ title }` | `200` `ConversationSummary` | 400 `errors.title`; 404 `NotFound` |
| `PUT` | `/api/conversations/{id}/folder` | `{ folderId }` (null: no folder) | `200` `ConversationSummary` | 400 `errors.folderId`; 404 `NotFound` |
| `DELETE` | `/api/conversations/{id}` | — | `204`, messages and images included | 404 `NotFound` |
| `GET` | `/api/conversations/{id}/images/{imageId}` | — | `200` the image bytes with their `Content-Type`, `Cache-Control: private, max-age=31536000, immutable`, `X-Content-Type-Options: nosniff` | 404 `NotFound` (also for an image of another conversation) |
| `POST` | `/api/conversations/{id}/messages/stream` | `{ question, images?, model? }` | `200` `text/event-stream` (below) | As `/api/chat/stream`, plus 404 `NotFound` and 409 `ConversationFull` |

Shapes:

```jsonc
// Folder
{ "id": "5f0c…", "name": "Work", "icon": "briefcase", "color": "blue", "conversationCount": 3, "createdAt": "2026-11-16T21:40:00+00:00" }

// ConversationSummary
{
  "id": "9b1e…", "title": "Plan the team offsite", "folderId": "5f0c…", "model": "gpt-6-luna", "questionCount": 2,
  "usage": { "inputTokens": 30, "outputTokens": 4, "totalTokens": 34 },
  "createdAt": "2026-11-16T21:41:00+00:00", "updatedAt": "2026-11-16T21:45:00+00:00"
}

// GET /api/conversations/{id}
{
  "conversation": { /* ConversationSummary */ },
  "messages": [
    { "id": "…", "role": "user", "text": "What is this?", "images": [{ "id": "…", "mediaType": "image/png", "size": 70, "url": "/api/conversations/9b1e…/images/…" }],
      "model": null, "usage": null, "truncated": false, "stopped": false, "createdAt": "…" },
    { "id": "…", "role": "assistant", "text": "A diagram.", "images": [], "model": "gpt-6-luna",
      "usage": { "inputTokens": 120, "outputTokens": 3, "totalTokens": 123 }, "truncated": false, "stopped": false, "createdAt": "…" }
  ]
}
```

Folder `icon`: `folder`, `briefcase`, `star`, `heart`, `book`, `code`, `lightbulb`, `rocket`, `people`, `home`, `beaker`, `airplane`. `color`: `default`, `red`, `orange`, `yellow`, `green`, `teal`, `blue`, `purple`, `berry`, `pink`, `brown` (`FolderAppearance`). The web app maps them to Fluent icons and theme colours.

`url` is relative to the API. The conversation's `usage` is the sum of its answers'; `model` is the model of the last answer.

### `POST /api/conversations/{id}/messages/stream`

The body is a chat request without `history`: the server reads the history from the database. Validation, the model check, the rate limit (`RateLimiting:ChatPermitsPerMinute`, shared with the chat) and the first-piece rule are those of [`POST /api/chat/stream`](#post-apichatstream), with the same events. `done` also carries the saved conversation:

```text
event: done
data: {"answer":"Hello Ana!","model":"gpt-6-luna","truncated":false,"usage":{…},"conversation":{ /* ConversationSummary */ }}
```

Saving:

- **Complete answer:** question (with its images) and answer are saved before `done` is sent. The first question names an untitled conversation.
- **Stopped** (the client closed the request): the text received so far is saved as the answer with `stopped: true` and no usage.
- **Failed** (a problem before the stream or an `error` event): nothing is saved.

## Errors

| Status | `failure` | When |
| ------ | --------- | ---- |
| 400 | — (`errors.question` / `errors.history` / `errors.images` / `errors.model`) | Invalid question, history or images (count, base64, size, format, declared type different from the content), unknown role, malformed JSON |
| 404 | `NotFound` | Saved chats: the folder, conversation or image does not exist |
| 405 | — | Any method other than `POST` on `/api/chat` |
| 409 | `DuplicateFolderName`, `ConversationFull` | Saved chats: a folder with that name exists; the conversation has 50 questions |
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
  "detail": "The model 'gpt-6-luna' is currently unavailable in the LiteLLM proxy (no healthy deployments).",
  "failure": "ModelUnavailable",
  "traceId": "00-56c0af94…-00"
}
```

`detail` follows the [chat service failures](../specs/feature-llm-chat.md#failures). It never contains the key; for 400/404/422 from the proxy it may quote the first line of the proxy's message.

## CORS

Only the origins in `Cors:AllowedOrigins` (`http://localhost:3030` in Development), methods `GET`, `POST`, `PATCH`, `PUT` and `DELETE` (the last three for saved chats), any header.

## Security notes

- No authentication: the API is for local use (`launchSettings.json` binds to `localhost`). The rate limit reduces the damage of misuse; an exposed deployment needs authentication first (see [todo.md](../tasks/todo.md)).
- The proxy key and endpoint never appear in answers or problems.
- Prompts and answers are not logged, nor is saved content.
- Saved chats have no owner: anyone who reaches the API reads them. Fine on localhost; authentication comes first otherwise.
- Saved images are served with their detected type and `nosniff`, so a browser never runs one as a page.

## Verification

- `ChatEndpointTests` (integration, `WebApplicationFactory` with a fake `IChatService`):
  - Answers: answer with model and `truncated`; history passed in order; trimmed question.
  - Images: decoded and passed to the model; 400 for invalid base64, a type mismatch, a non-image, missing fields, too many and too large (rejected before decoding).
  - Validation: 400 for an empty, missing or too-long question; empty, role-less or null turns; history over the limit; `"system"` or numeric roles; malformed JSON. None of them calls the model.
  - Failures: 503 when not configured; the status, `failure`, `title` and `detail` of every `ChatFailure`.
  - Limits and CORS: 429 with `Retry-After` over the rate limit, the CORS preflight for `POST`, 405 for `GET`.
- `UsageEndpointTests` (integration): `usage` on `/api/chat`, on the `done` event and on `/api/images`; null without it.
- `ImageEndpointTests` (integration): base64 answer and size; 400 for an empty prompt or an unknown size; 503 when not configured; failures; its own rate limit.
- `ModelsEndpointTests` (integration):
  - `GET /api/models`: the list and its limits; 503 when not configured; the proxy's failure.
  - `model` on both chat endpoints: a chat model passes; the default reads no list; a non-chat or unknown model gets 400; a failure to read the list.
- `ChatStreamEndpointTests` (integration):
  - Events: `delta`s then `done`, `text/event-stream`, the same input as `/api/chat`.
  - Errors: 400 and 503 as ProblemDetails, not a stream; failures before the first piece with their status and `failure`; a failure mid-answer as an `error` event.
  - The shared rate limit.
- `ConversationEndpointTests` (integration, a temporary SQLite file per client): see [saved chats](../specs/feature-saved-chats.md#verification).
- `HealthEndpointTests`: health answer, no secrets, CORS.
- Manual: `DotNextAI.Api.http`, or `curl` against `./scripts/dev.sh`.
