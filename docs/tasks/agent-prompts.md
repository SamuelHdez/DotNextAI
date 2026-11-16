# Agent prompts

History of the prompts that shaped DotNextAI, with the outcome of each. Newest last. Paraphrased in English; the originals were in Spanish.

## 2026-11-16 — Course analysis

> Analyze the course exercise *Project 1: Basic LLM Chatbot App* (ten steps in three phases) for the DotNextAI repository. Analysis only.

Outcome: the repository was empty. A step-by-step mapping was proposed, and the open choices (LLM provider, web stack, runtime) were raised.

## 2026-11-16 — Stack change

> The course now allows any backend. Use .NET with Semantic Kernel instead?

Outcome: .NET was confirmed as a good fit. Microsoft Agent Framework was recommended over Semantic Kernel as its successor.

## 2026-11-16 — Stack and repository shape

> Use Microsoft Agent Framework. The frontend will be Next.js + React + Fluent UI. Add GitHub Actions CI/CD. The repository layout and pipelines must match ValidatorHub and ctf-janus.

Outcome: both reference repos were analyzed, and a layout plus the list of pipelines to mirror were proposed. Decisions were recorded in [architecture](../design/architecture.md#decisions-log).

## 2026-11-16 — Step 00: backend skeleton and docs

> ERNI LiteLLM proxy with URL and token. Playwright E2E: yes. Code, docs and commits in English, multi-language UI. Working from Windows. Add a `dev.sh`. Go step by step: first only the backend project setup, `AGENTS.md` and the documentation.

Outcome: step 00 in [todo.md](todo.md). The backend solution builds with 0 warnings, all tests pass, and `dev.sh` and the docs are written. CI, frontend and Docker are left for later steps.

## 2026-11-16 — Initial commit and backend CI

> Commit, then set up the pipelines.

Outcome: initial commit `be84b7c` (the user pushes it). Added `backend-ci.yml`, `security.yml`, `dependabot.yml` and `backend/Dockerfile`, mirroring ValidatorHub and ctf-janus. Checked locally with zizmor 1.30.1, Semgrep 1.180.0 (CI rulesets) and Gitleaks 8.30.1: no findings. The Docker job was not run locally.

## 2026-11-16 — Step 01: terminal Q&A

> CI is green. Go with step 01, the terminal. Document all the specs. Even with a web app later, the terminal project must persist.

Outcome: `dotnextai ask` through the LiteLLM proxy with the OpenAI .NET SDK, behind an `IChatService` contract the API will reuse. New specs: [CLI](../specs/feature-cli.md) (permanent deliverable, with a roadmap for later steps) and [chat service](../specs/feature-llm-chat.md). The first real call showed that every chat model of the proxy had *"no healthy deployments"*, so that error got its own failure type and the proxy's error detail is now shown (key masked). 68 tests green; a real answer is still to be confirmed.

## 2026-11-16 — Step 02: Agent Framework

> Go with step 02.

Outcome: the question now goes through a Microsoft Agent Framework `ChatClientAgent` (1.28.0, on `Microsoft.Extensions.AI` 10.11.0, so still the .NET 10 line), with fixed instructions and no session. The CLI and the `IChatService` contract are unchanged; client creation and error mapping were extracted for reuse. 70 tests green. The proxy still had no healthy chat deployments, so a real answer is still to be confirmed.

## 2026-11-16 — Step 03: chat history

> Go (with the defaults proposed for leaving the chat, chat commands and a limit).

Outcome: `dotnextai` (and `dotnextai chat`) is a multi-turn chat with `/new`, `/help`, `/exit`, `/quit` and Ctrl+D, capped at 50 questions. The history is kept by the front end and sent with every question (`IChatService.AskAsync(question, history)`, `ChatConversation`), not in an `AgentSession`, so the step 04 API can stay stateless. 97 tests green; the proxy still had no healthy chat deployments.

## 2026-11-16 — Step 04: API

> Go with step 04.

Outcome: `POST /api/chat`, stateless (question + history), over the same `IChatService` as the CLI. Errors are `ProblemDetails` with a `failure` code for the multi-language web app, and the proxy's 401/403 become 502. Added unprompted: a per-IP rate limit (20/min, configurable), because the endpoint is unauthenticated and spends the server's key. New contract document `docs/design/api.md`. 123 tests green; a live call through the API returned the proxy's real `ModelUnavailable` as a 503.

## 2026-11-16 — Step 05: web app

> Those languages are fine; the language is changed with a selector. Build a dashboard structure like ValidatorHub's, with Fluent UI and the ERNI colours.

Outcome: `frontend/` with Next.js 16 and Fluent UI v9. The ValidatorHub shell, ERNI theme, Griffel SSR and security headers are reused. A typed English/Spanish i18n with a cookie-backed selector. Home page: service status and course backlog. Chat page over `POST /api/chat`, with errors from the `failure` code. Also `frontend-ci.yml`, the Dockerfile, Dependabot npm, and `dev.sh` starting the API and the web together. All npm versions at least 7 days old (`--before`). 184 Vitest tests, lint and type-check clean, production build OK. Manually checked in light and dark mode, and in English and Spanish rendered on the server.

## 2026-11-16 — E2E with Playwright

> OK (go with the E2E suite).

Outcome: Playwright suite in `frontend/e2e` with 15 scenarios and axe on every page. It runs against a fake OpenAI-compatible LiteLLM proxy instead of a fake service inside the API, so the whole chain is tested with no test code in production and no secrets. The run found and fixed two accessibility bugs: label-in-name on the language button, and list semantics in the chat. Added `e2e.yml`, `./scripts/dev.sh e2e` and `docs/design/testing.md`.

## 2026-11-16 — Step 06: image input

> Assume the model accepts images; go with it.

Outcome: images in every front end (web button and paste with previews, CLI `--image` and `/image`, API `images` in base64), sent to the agent as `DataContent`. Rules shared by every front end: up to 4 per question, 4 MB each, format from the file signature. Decision: images go only with the question they were attached to, and the history stays text, to keep requests small. 160 .NET tests, 209 Vitest tests, 17 Playwright scenarios (the fake LiteLLM now checks image data URIs). Also run manually through the CLI against the fake proxy.

## 2026-11-16 — Step 07: streaming

> Yes (go with streaming).

Outcome: `IChatService.AskStreamingAsync` over Agent Framework's `RunStreamingAsync`, and `POST /api/chat/stream` with Server-Sent Events. The first piece is awaited before streaming, so the common failures keep their status codes; failures mid-answer come as an `error` event. The web app has its own SSE reader, the answer grows as it is written, there is a Stop button, and `aria-busy` while streaming. The CLI prints the pieces as they arrive. The fake LiteLLM now streams real `chat.completion.chunk` events, with slow and dropped-connection modes. 182 .NET tests, 224 Vitest tests and 19 Playwright scenarios.

## 2026-11-16 — Step 08: model selector

> Yes (go with the model selector).

Outcome: `IModelCatalog` reads the proxy's model list. The real ERNI proxy reports `mode` and token limits on `/models`; the list is filtered to chat models and cached for 5 minutes. `ChatRequest` carries an optional model, which is accepted only if listed, with one agent per model. New `GET /api/models`; the web has a remembered selector, the CLI has `models`, `--model` and `/model`. A test caught a re-mount bug in the selector's failure state, which was fixed. 222 .NET tests, 235 Vitest tests and 20 Playwright scenarios.

## 2026-11-16 — LiteLLM `/v2/model/info`

> There is a `/v2/model/info` endpoint that returns these models (gpt-6.1-sol, gpt-6-astra, gpt-6-luna, gpt-6.1-terra, gpt-image-2, …).

Outcome: probing with our key explained the outage. The key's team only reaches `gpt-5.6-*`, which has no healthy deployments, and the live `gpt-6.x` models answer 403 `team_model_access_denied`. The catalog keeps `/models` because it is filtered by the key's access, while `/v2/model/info` lists models the key cannot use. That 403 now maps to `ModelNotFound` ("no access to the model") instead of the misleading "rejected the API key". Follow-up: ask the proxy administrator for access to `gpt-6.x`.

## 2026-11-16 — Steps 09 and 10

> Yes, in fact go on with both: do step 09 and commit, then go straight to step 10 and commit.

Step 09 outcome: `IImageService` calls `POST images/generations` directly, because gpt-image only answers base64 and rejects `response_format`. There are no retries, `Llm:ImageModel` is the model, and the endpoint has its own 5-per-minute limit. The web has an *Images* page (shape, gallery, download) and the CLI has `dotnextai image` (saves the file and refuses to overwrite without `--force`, checked before generating). The fake LiteLLM draws a real PNG. 265 .NET tests, 264 Vitest tests and 23 Playwright scenarios.

Step 10 outcome: `TokenUsage` on answers and images, taken from the provider's counters. A probe showed that the SDK already asks for `stream_options.include_usage`, so streamed answers get their usage from the final chunk. The conversation total is the sum of the calls, with a hint that input grows with the history. Web captions and total, a CLI `Tokens:` line and `/usage`, `usage` in the API. The fake LiteLLM estimates usage that grows with the history. 287 .NET tests, 274 Vitest tests and 24 Playwright scenarios. The whole course backlog is done.
