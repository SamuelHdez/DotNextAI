# Architecture

> **Status:** Step 07 implemented on 2026-11-16 (streamed answers in the web, the CLI and the API), on top of steps 00-06 (backend skeleton, CI, CLI with chat history, Agent Framework agent, chat API, web app, images). Components marked *planned* arrive with later backlog steps.

## Components

```mermaid
flowchart LR
    user([User])
    cli["DotNextAI.Cli<br/>terminal chatbot"]
    web["frontend<br/>Next.js + Fluent UI<br/>:3030"]
    api["DotNextAI.Api<br/>ASP.NET Core Minimal API<br/>:5100"]
    infra["DotNextAI.Infrastructure<br/>Agent Framework + OpenAI SDK"]
    litellm[("ERNI LiteLLM proxy<br/>OpenAI-compatible")]

    user --> cli
    user --> web
    web -- "HTTP / SSE (CORS)" --> api
    cli --> infra
    api --> infra
    infra -- "HTTPS + virtual key" --> litellm
```

| Component | Responsibility |
| --------- | -------------- |
| `DotNextAI.Api` | HTTP surface: `GET /api/health`, `POST /api/chat` (step 04) with per-IP rate limiting ([API contract](api.md)). Planned: streaming (SSE), models, image generation. OpenAPI document at `/openapi/v1.json` and Swagger UI at `/swagger` in Development. |
| `DotNextAI.Application` | Contracts with no dependencies: DTOs (`HealthStatusDto`), the chat contract (`IChatService`, `ChatTurn`, `ChatAnswer`, `ChatFailure`, `ChatServiceException`, `ChatRules`), `ChatConversation` (history kept by the front end) and, later, images. |
| `DotNextAI.Infrastructure` | Configuration (`LlmOptions`) and the model clients. `LlmChatClientFactory` builds the OpenAI .NET SDK `ChatClient` pointed at the proxy over the named `HttpClient` `Llm`. `AgentChatService` (step 02) wraps it as a Microsoft Agent Framework `ChatClientAgent`. `ChatFailureMapper` maps errors ([chat service spec](../specs/feature-llm-chat.md)). |
| `DotNextAI.Cli` | Terminal app `dotnextai`, a permanent deliverable: System.CommandLine commands (`chat`, the default, and `ask`) over `IChatService`. It uses the same Infrastructure as the API, so the logic is written once ([CLI spec](../specs/feature-cli.md)). |
| `frontend` | Next.js 16 + Fluent UI v9 dashboard (ValidatorHub shell, ERNI brand, English/Spanish): home with service status and backlog, chat with the history in the browser. Calls only `DotNextAI.Api`, from the browser ([web app spec](../specs/feature-web-app.md)). |

Project references: `Api → Application, Infrastructure`; `Cli → Application, Infrastructure`; `Infrastructure → Application`. Application references nothing.

## Ports

| Service | URL |
| ------- | --- |
| API | `http://localhost:5100` (`launchSettings.json`, profile `http`) |
| Web app | `http://localhost:3030` (`next dev --port 3030`; the Docker image listens on 3030), allowed by CORS in Development |

## API

| Method | Path | Purpose |
| ------ | ---- | ------- |
| `GET` | `/api/health` | Liveness and whether the LLM proxy is configured |
| `POST` | `/api/chat/stream` | Same, with the answer as Server-Sent Events (`delta`, `done`, `error`) |
| `POST` | `/api/chat` | Question + history → answer (stateless, rate-limited) |

Full contract, status codes and the `failure` extension: [API contract](api.md). Errors use `ProblemDetails` (`AddProblemDetails`, `UseExceptionHandler`, `UseStatusCodePages`).

## Configuration

`LlmOptions` (section `Llm`) is bound with `ValidateDataAnnotations` and `ValidateOnStart`. Missing values do not stop the startup, so the API runs and `llmConfigured` is false. An out-of-range `TimeoutSeconds` or `MaxRetries` does stop it. The secrets live in `dotnet user-secrets`, in one store shared by the API and the CLI (same `UserSecretsId`). The CLI runs in `Development` by default so it loads that store. The integration tests use the `Testing` environment so they never load it. See the [README](../../README.md#configuration) for the setting list.

## Build and quality

- `backend/Directory.Build.props`: `net10.0`, nullable, .NET analyzers `latest-recommended` with the security rules set to `All`, `NuGetAudit` (vulnerable packages fail the build), and `packages.lock.json` with locked-mode restore when `ContinuousIntegrationBuild=true`.
- `global.json` (repository root, so it applies from the root and from `backend/`): SDK 10.0.100 or a later feature band; tests run on Microsoft.Testing.Platform (xUnit v3).
- `backend/tests/Directory.Build.props`: test projects are executables; `CA1707` allowed for `Method_Scenario_Expected` names.
- `backend/Dockerfile`: multi-stage image of the API (SDK and ASP.NET base images pinned by digest, locked restore, non-root `$APP_UID`, port 8080, `Production`). The LLM settings come from environment variables at run time. The CLI has no image.
- `frontend/Dockerfile`: multi-stage Next.js standalone image (`node:24-alpine` pinned by digest, `npm ci --ignore-scripts`, npm/corepack removed from the runtime image, non-root `node` user, port 3030). `NEXT_PUBLIC_API_BASE_URL` is a build argument.
- CI, mirroring ValidatorHub and ctf-janus (see the [README](../../README.md#ci)): `backend-ci.yml`, `frontend-ci.yml` and `security.yml`, with Dependabot (npm, NuGet, Docker, Actions) on a 7-day cooldown. `e2e.yml` runs Playwright against the production web build, the real API and a fake LiteLLM proxy ([testing](testing.md)).

## Decisions log

| Date | Topic | Decision | Reason |
| ---- | ----- | -------- | ------ |
| 2026-11-16 | Runtime | .NET 10 (LTS), ASP.NET Core and `Microsoft.Extensions.*` packages on the 10.0.x line. | The installed SDK is 10.0.301. ValidatorHub and ctf-janus use the same line. .NET 11 packages exist but are not LTS. |
| 2026-11-16 | Orchestration | Microsoft Agent Framework (`Microsoft.Agents.AI`, 1.x stable) on Microsoft.Extensions.AI, instead of LangChain or Semantic Kernel. | It is Microsoft's successor to Semantic Kernel and AutoGen. `IChatClient` gives streaming, multimodal content (`DataContent`) and usage (`UsageDetails`) out of the box. |
| 2026-11-16 | Direct calls (step 01) | OpenAI .NET SDK with its endpoint set to the LiteLLM proxy. | LiteLLM is a Python library; from .NET, its OpenAI-compatible proxy is the way to use it. |
| 2026-11-16 | API style | Minimal API with `TypedResults`, not controllers as in ValidatorHub and ctf-janus. | Small surface, and native SSE (`TypedResults.ServerSentEvents`) for streaming. |
| 2026-11-16 | Layering | ValidatorHub's `backend/src` + `backend/tests` layout without a `Domain` project. | There are no persisted entities. A `Domain` layer can be added if one appears. |
| 2026-11-16 | Secrets | `dotnet user-secrets`, one store shared by the API and the CLI, written by `dev.sh secrets` through stdin. | Same practice as the reference repos. One setup serves both executables, and the key never shows up in a process list. |
| 2026-11-16 | Tests | xUnit v3 on Microsoft.Testing.Platform, `WebApplicationFactory` in the `Testing` environment, and fake model clients. | Same as ValidatorHub. Tests and CI need no proxy and no secrets. |
| 2026-11-16 | Web app | Next.js + React + Fluent UI v9, port 3030 (planned). | Same stack as the ValidatorHub frontend and the ctf-janus admin. The port avoids clashing with them (3000, 3010). |
| 2026-11-16 | CI | `backend-ci.yml` = ValidatorHub's build job (MTP `--solution` tests) + ctf-janus's Docker job (Trivy binary verified by checksum, not `trivy-action`). `security.yml` copied unchanged (identical in both repos). | Same guarantees as the reference repos. ctf-janus moved away from `trivy-action` after it was compromised in 2026. |
| 2026-11-16 | Dependabot | Ignore semver-major updates of `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*` and the `dotnet/*` base images. | Keeps the backend on the .NET 10 LTS line. A move to 11.x is a manual change of the target framework, packages and images together. |
| 2026-11-16 | Model call (step 01) | `IChatService` in Application, implemented by `OpenAiChatService` (OpenAI SDK `ChatClient`) in Infrastructure. The SDK's transport is an `IHttpClientFactory` client. | The CLI and the API share one implementation, step 02 can swap it for Agent Framework without touching the front ends, and tests stub HTTP instead of calling the proxy. |
| 2026-11-16 | Proxy errors | Quote the first line of LiteLLM's `error.message` for 400/404/422 (key masked, max 300 characters). "No healthy deployments" maps to its own `ModelUnavailable` failure. | A status-only message ("rejected the request as invalid") hid the real cause when the proxy's models were down on the first run. |
| 2026-11-16 | CLI | System.CommandLine 2.0.15 (.NET 10 servicing line), invariant culture, UTF-8, logs off by default, stdout for answers only. | A permanent, scriptable terminal app with consistent English texts. 3.0.0 is the .NET 11 line. |
| 2026-11-16 | Package age | Microsoft .NET servicing packages (10.0.x, System.CommandLine 2.0.x) are taken at the latest patch. Third-party packages must be at least 7 days old (OpenAI 2.15.0: 2026-11-05). | Servicing releases carry security fixes; the 7-day cooldown targets compromised third-party releases. |
| 2026-11-16 | Orchestration (step 02) | `AgentChatService` replaces `OpenAiChatService`: `ChatClient.AsAIAgent(instructions, name)` from `Microsoft.Agents.AI.OpenAI` 1.28.0, a stateless `RunAsync` per question. Client creation and error mapping moved to `LlmChatClientFactory` and `ChatFailureMapper`. | One code path that later steps extend (sessions, streaming, usage), and front ends are untouched. Agent Framework 1.28.0 (2026-11-06) passed the 7-day rule and depends on `Microsoft.Extensions.AI` 10.11.0, so the .NET 10 line holds. |
| 2026-11-16 | Chat history (step 03) | Stateless `IChatService.AskAsync(question, history)`; the front end keeps the turns (`ChatConversation` in the CLI, the browser for the web app). No `AgentSession`. Limit of 50 questions per conversation. | The API stays stateless (no per-user storage or affinity) and the rule "conversations live in the CLI process or the browser" holds. The limit bounds the cost of resending the history. |
| 2026-11-16 | Chat API (step 04) | `POST /api/chat` over `IChatService`, stateless. `ProblemDetails` with a `failure` extension; proxy 401/403 become 502. Built-in ASP.NET Core rate limiter: a fixed window per IP on the chat endpoint, 20 per minute by default. | One implementation for the CLI and the web. The `failure` code lets the multi-language UI translate errors. The unauthenticated endpoint spends the server's key, so it is capped, as ValidatorHub caps its sensitive endpoints. |
| 2026-11-16 | Web app (step 05) | Next.js 16 + Fluent UI v9 in `frontend/`, the ValidatorHub shell and ERNI theme files reused. Own typed i18n (English default, Spanish) with a cookie, browser → API directly, per-request CSP nonce. | The same look and the same hardening as the other ERNI apps. The i18n is small enough not to need a library. Direct calls keep the API the only holder of the key and make step 07's streaming straightforward. |
| 2026-11-16 | npm versions | `npm install --before=<7 days ago>` for the whole tree (direct and transitive). The dev-only ESLint chain keeps 9 moderate `minimatch` advisories whose fixes are younger than that; CI blocks production on high+ and everything on critical, as in the reference repos. | The 7-day cooldown is the supply-chain rule of every ERNI repo here; a moderate dev-tool advisory is the lesser risk. |
| 2026-11-16 | E2E | Playwright starts a fake OpenAI-compatible LiteLLM (`frontend/e2e/fake-litellm.mjs`), the real API in the `E2E` environment and the production web build on their own ports. axe on every page with no violation allowed (Tabster sentinels excluded). | It tests the whole chain without test code in the API, without secrets and next to a running `dev.sh`. The axe rule set matches the ctf-janus admin suite. |
| 2026-11-16 | Image input (step 06) | `ChatImage` and `ImageFormats` (signature detection) in Application, the same rules mirrored in the web app. Base64 in the API (`images`), `DataContent` in the agent. Images go only with the question they were attached to. | One rule set in every front end. The bytes decide the format, not names or declared types. Not resending images keeps requests small and image tokens bounded. |
| 2026-11-16 | Streaming (step 07) | `IChatService.AskStreamingAsync` over `RunStreamingAsync`. `POST /api/chat/stream` with `TypedResults.ServerSentEvents` waits for the first piece before starting the stream. The web app uses its own SSE reader over `fetch`; the CLI writes the pieces to stdout. | Errors keep their status codes in the common case, and the mid-answer case keeps the same `failure` codes. `EventSource` cannot POST a body, and a 30-line reader avoids a dependency. |
