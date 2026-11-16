# API contract

> **Status:** Step 06 (images in the chat endpoint) implemented on 2026-11-16, on top of step 04 (chat endpoint). The web app mirrors these contracts in `frontend/src/services/types.ts`; keep both in sync.

`DotNextAI.Api` is an ASP.NET Core 10 Minimal API on `http://localhost:5100`. It exposes the same chat service as the CLI ([chat service](../specs/feature-llm-chat.md)) and is meant for local use by the web app. In Development the OpenAPI document is at `/openapi/v1.json` and Swagger UI at `/swagger`; `backend/src/DotNextAI.Api/DotNextAI.Api.http` has ready-made requests.

## Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| State | Stateless. The client keeps the conversation and sends its history with every question, as the CLI does. No sessions, no storage. | 2026-11-16 |
| Errors | `ProblemDetails` (`application/problem+json`). Validation errors are a 400 `ValidationProblem` with `errors` per field. Model failures carry a `failure` extension with the `ChatFailure` name, so the multi-language web app maps it to its own text; `detail` is English and only for logs and developers. | 2026-11-16 |
| Status codes | Problems on the server side or in the proxy are 5xx: 502 when the proxy rejects or fails, 503 when the model is unavailable or not configured, 504 on timeout. 429 for rate limits, either the proxy's or the API's own. 422 when the proxy refuses the content, e.g. context window exceeded. 401/403 from the proxy are **not** forwarded: the key belongs to the server, not the caller. | 2026-11-16 |
| Rate limit | `POST /api/chat` has a fixed window per client IP: `RateLimiting:ChatPermitsPerMinute`, default 20, range 1-10,000. The API has no authentication and every call spends the LiteLLM key, so this caps a runaway script or an exposed port. | 2026-11-16 |
| JSON | camelCase properties; enums as camelCase strings (`"user"`, `"assistant"`), integers rejected. | 2026-11-16 |
| Contracts | HTTP DTOs live in `DotNextAI.Api/Contracts` (`ChatRequestDto`, `ChatTurnDto`, `ChatResponseDto`) and map to the Application types; `HealthStatusDto` is in Application. | 2026-11-16 |

## Endpoints

### `GET /api/health`

`200`:

```json
{ "status": "Healthy", "service": "DotNextAI.Api", "environment": "Development", "llmConfigured": true, "timestampUtc": "2026-11-16T16:42:34.33+00:00" }
```

`llmConfigured` is true when endpoint, key and default model are set. The answer never contains the endpoint or the key.

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
| `images` | Optional (step 06). Images for **this question only**; the history stays text. At most 4. `mediaType` is `image/png`, `image/jpeg`, `image/webp` or `image/gif` and must match the bytes; `data` is the file in base64 (no `data:` prefix), at most 4 MB decoded. Oversized data is rejected before it is decoded. |

`200`:

```json
{ "answer": "Your name is Ana.", "model": "gpt-5.6-luna", "truncated": false }
```

`truncated` is true when the answer stopped at the maximum output length. To continue the conversation, the client appends `{ role: "user", text: question }` and `{ role: "assistant", text: answer }` to its history. The question goes as sent; the server trims it.

## Errors

| Status | `failure` | When |
| ------ | --------- | ---- |
| 400 | — (`errors.question` / `errors.history` / `errors.images`) | Invalid question, history or images (count, base64, size, format, declared type different from the content), unknown role, malformed JSON |
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
- `HealthEndpointTests`: health answer, no secrets, CORS.
- Manual: `DotNextAI.Api.http`, or `curl` against `./scripts/dev.sh`.
