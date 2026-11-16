# To-do / backlog

`- [ ]` pending, `- [x] YYYY-MM-DD — …` done. The course steps are described in [product rules](../specs/product-rules.md#backlog).

## Step 00 — Repository and backend skeleton

- [x] 2026-11-16 — Root files: `.gitignore`, `.gitattributes`, `.editorconfig`, `nuget.config`.
- [x] 2026-11-16 — Backend solution `backend/DotNextAI.slnx` (Api, Application, Infrastructure, Cli, unit and integration tests), `Directory.Build.props` (analyzers, NuGet audit, lock files), root `global.json` (SDK line, Microsoft.Testing.Platform).
- [x] 2026-11-16 — `LlmOptions` (LiteLLM endpoint, key, default model) with user-secrets shared by the API and the CLI.
- [x] 2026-11-16 — `GET /api/health`, OpenAPI + Swagger UI, CORS for the future web app.
- [x] 2026-11-16 — `scripts/dev.sh` (api, cli, secrets, build, test, status).
- [x] 2026-11-16 — `AGENTS.md`, `CLAUDE.md`, `README.md`, `docs/` (product rules, architecture, tasks).

## Repository and CI

- [x] 2026-11-16 — `backend-ci.yml`: Semgrep, locked restore, vulnerable-package check, `-warnaserror` build, tests with coverage, API image with SBOM and Trivy scan (same as ValidatorHub and ctf-janus).
- [x] 2026-11-16 — `security.yml`: Gitleaks and zizmor.
- [x] 2026-11-16 — `.github/dependabot.yml`: NuGet, Docker and GitHub Actions with a 7-day cooldown. Add npm with the frontend.
- [x] 2026-11-16 — Backend `Dockerfile` (digest-pinned images, non-root user).
- [ ] Confirm the first `backend-ci.yml` and `security.yml` runs on GitHub are green (the Docker job has not been run locally: no Docker on the dev machine).
- [x] 2026-11-16 — `frontend-ci.yml` (Semgrep, npm audits, lint, type-check, Vitest, build, web image with SBOM and Trivy), `frontend/Dockerfile`, npm and `/frontend` Docker in Dependabot.
- [x] 2026-11-16 — `e2e.yml` + `./scripts/dev.sh e2e`: Playwright against a fake LiteLLM proxy, the real API and the production web build; axe on every page (two real findings fixed) ([testing](../design/testing.md)).
- [ ] Branch protection on `main` and secret scanning with push protection in the GitHub repository settings.

## Phase 1 — Backend basics

- [x] 2026-11-16 — 01 Terminal Q&A: `dotnextai ask` (argument, stdin or prompt) through the proxy with the OpenAI .NET SDK; `IChatService` contract, failure mapping, 20,000-character limit, exit codes ([CLI](../specs/feature-cli.md), [chat service](../specs/feature-llm-chat.md)).
- [ ] 01-04 follow-up: confirm a real answer (and a remembered name across two chat turns) from the proxy, in the CLI and through `POST /api/chat`. On 2026-11-16 every chat model answered *"no healthy deployments"* (proxy side); the CLI shows it as `ModelUnavailable`.
- [x] 2026-11-16 — 02 Agent Framework: `AgentChatService` (`ChatClientAgent` with instructions, stateless run) replaces the direct call; `LlmChatClientFactory` and `ChatFailureMapper` extracted ([chat service](../specs/feature-llm-chat.md#agent)).
- [x] 2026-11-16 — 03 Chat history: `dotnextai` / `dotnextai chat` with in-memory history (`ChatConversation`), `/new`, `/help`, `/exit`, 50-question limit; stateless `IChatService.AskAsync(question, history)` ([CLI](../specs/feature-cli.md), [chat service](../specs/feature-llm-chat.md#conversation-history)).
- [x] 2026-11-16 — 04 API: `POST /api/chat` (question + history), `ProblemDetails` with a `failure` code per `ChatFailure`, rate limit per IP (20/min), OpenAPI ([API contract](../design/api.md)).
- [ ] Before exposing the API beyond localhost: authentication (e.g. Entra ID, as ValidatorHub) and HTTPS.

## Phase 2 — Web app

- [x] 2026-11-16 — 05 Web UI: `frontend/` Next.js 16 + Fluent UI v9 with the ValidatorHub shell and ERNI theme, English/Spanish selector, home (service status, backlog) and chat over `POST /api/chat` ([web app](../specs/feature-web-app.md)).
- [ ] 05 follow-up: render answers as Markdown with a sanitizing renderer.
- [ ] 05 follow-up: remove the 9 moderate dev-only `minimatch` advisories (ESLint chain) once the fixed versions pass the 7-day cooldown (Dependabot).
- [x] 2026-11-16 — 06 Image input: up to 4 images (PNG/JPEG/WebP/GIF, 4 MB) per question in the web (button, paste), the CLI (`--image`, `/image`) and the API (`images`, base64), `DataContent` to the agent; format from the bytes; images go with their question only ([chat service](../specs/feature-llm-chat.md#images)).
- [x] 2026-11-16 — 07 Streaming: `POST /api/chat/stream` (SSE `delta`/`done`/`error`, failures before the first piece as ProblemDetails), `AskStreamingAsync` over the agent's `RunStreamingAsync`, web rendering with Stop and `aria-busy`, CLI printing the pieces ([API](../design/api.md#post-apichatstream)).

## Phase 3 — Extra features

- [x] 2026-11-16 — 08 Model selector: `GET /api/models` (proxy chat models, 5-minute cache), optional `model` checked against the list, one agent per model; web selector (remembered), CLI `models`, `--model`, `/model` ([API](../design/api.md#get-apimodels)).
- [x] 2026-11-16 — 09 Image generation: `POST /api/images` (`gpt-image-2`, direct `images/generations`, no retries, 5/min per IP), the *Images* page with gallery and downloads, `dotnextai image` ([image generation](../specs/feature-images.md)).
- [ ] 09 follow-up: try `gpt-image-2` against the real proxy once the key's access is sorted out.
- [x] 2026-11-16 — 10 Token metrics: the proxy's usage for every answer (streamed too) and image; conversation total = sum of the calls; web captions and total, CLI `Tokens:` line and `/usage`, API `usage` ([token metrics](../specs/feature-token-metrics.md)).

## After the course — saved chats

- [x] 2026-11-16 — Saved chats part 1 (backend): `DotNextAI.Persistence` (EF Core 10 + SQLite, `InitialSavedChats` migration applied at startup), `IConversationStore` and `ConversationService`, `/api/folders` and `/api/conversations` (CRUD, images, streamed ask with the saved history), CORS for `PATCH`/`PUT`/`DELETE`, migrations checked in CI, Docker volume `/app/data` ([saved chats](../specs/feature-saved-chats.md)).
- [x] 2026-11-16 — Saved chats part 2 (web): *Saved chats* panel on the chat page (folders, chats, rename, move, delete in dialogs), every chat saved by its first question and asked through `POST /api/conversations/{id}/messages/stream`, `/chat?c=<id>` to reopen, no empty chats left on failure, `img-src` for the API's images; Vitest (`ChatWorkspace`, `savedChats`) and 7 Playwright scenarios with axe ([saved chats](../specs/feature-saved-chats.md#web-app)).
- [ ] After 2026-11-21: update `@asamuzakjp/dom-selector` to 10.0.6 (jsdom `:focus-within` fix) and remove the focus workaround in `ChatWorkspace.test.tsx` ([testing](../design/testing.md#known-test-environment-issue-2026-11-16)).
- [ ] Saved chats follow-up: search, and an owner per row once the API has authentication.

## Open questions

- [x] 2026-11-16 — UI languages: English (default) and Spanish, with a selector.
- [x] 2026-11-16 — Models of the ERNI LiteLLM proxy: chat `gpt-5.6-luna` (default) and `gpt-5.6-terra`, image `gpt-image-2` (see [product rules](../specs/product-rules.md#models-available-through-the-proxy)).
- [ ] **Ask the ERNI proxy administrator** to give our key's team access to `gpt-6.x` (`gpt-6-luna`, `gpt-6.1-terra`…): today the key only reaches `gpt-5.6-*`, which has no healthy deployments, and `gpt-6.x` answers 403 `team_model_access_denied` (see [product rules](../specs/product-rules.md#models-available-through-the-proxy)). Then set `Llm:DefaultModel`.
- [ ] Confirm image input with the real model (`/v2/model/info` reports `supports_vision: true` for `gpt-6.x`).
- [x] 2026-11-16 — CLI language: English only (invariant culture). Multi-language is for the web UI.
