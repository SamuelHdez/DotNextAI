# Product rules

> **Status:** Active. Defined 2026-11-16.

DotNextAI is the ERNI course exercise *Project 1: Basic LLM Chatbot App*: build an LLM chatbot step by step, starting with a terminal script, then turn it into a local web app. The code is pushed to a personal GitHub repository and the link is shared in the course chat. The course allows any backend stack.

## Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| Backend | .NET 10 + Microsoft Agent Framework instead of the course's suggested Python + LangChain + FastAPI. | 2026-11-16 |
| LLM access | Only through ERNI's LiteLLM proxy (OpenAI-compatible API), with a URL and a virtual key. | 2026-11-16 |
| Web app | Next.js + React + Fluent UI v9 (the course suggests Streamlit or React). | 2026-11-16 |
| UI languages | Web UI in English (default) and Spanish, switched with a selector in the header and remembered in a cookie ([web app](feature-web-app.md)). The CLI stays English only. | 2026-11-16 |
| Code language | Code, comments, docs and commit messages in English. | 2026-11-16 |
| Delivery | Small increments, one backlog step (or less) at a time, each with docs and tests. | 2026-11-16 |
| Storage | **Reverses "no persistence for now".** The web app's conversations are saved, with their images, in a local SQLite database and can be put in folders (one level). A single local user, no login. The CLI keeps its conversations in memory ([saved chats](feature-saved-chats.md)). | 2026-11-16 |
| Terminal app | The CLI (steps 01-03) is a permanent deliverable, not a prototype replaced by the web app. It keeps working, and later features are added to it where they make sense ([CLI spec](feature-cli.md#roadmap)). | 2026-11-16 |

## Backlog

The course backlog, with how this repository maps each step. The spec of each step (`feature-*.md`) is written when the step starts.

| # | Phase | Step | Course wording | In DotNextAI |
| -- | ----- | ---- | -------------- | ------------ |
| 01 | Backend basics | Terminal Q&A app | Single question and answer with direct OpenAI API calls (through LiteLLM). | **Done 2026-11-16.** `dotnextai ask` calls the proxy with the OpenAI .NET SDK (`ChatClient`), with no orchestration layer ([CLI](feature-cli.md), [chat service](feature-llm-chat.md)). |
| 02 | Backend basics | Orchestration framework | Call the LLM through LangChain. | **Done 2026-11-16.** Same call through a Microsoft Agent Framework agent (`ChatClientAgent`, with instructions) behind `IChatService` ([chat service](feature-llm-chat.md#agent)). |
| 03 | Backend basics | Chat history | Add chat history to the conversation. | **Done 2026-11-16.** `dotnextai chat`: multi-turn conversation; the front end keeps the history and sends it with every question ([CLI](feature-cli.md), [chat service](feature-llm-chat.md#conversation-history)). |
| 04 | Backend basics | API | Encapsulate the LLM logic into an API. | **Done 2026-11-16.** `POST /api/chat` (question + history → answer) over the same `IChatService` as the CLI ([API contract](../design/api.md)). |
| 05 | Web app | Web UI | Streamlit or React, run locally. | **Done 2026-11-16.** `frontend/`: Next.js + Fluent UI dashboard (ValidatorHub layout, ERNI brand, English/Spanish) with a chat over `POST /api/chat` ([web app](feature-web-app.md)). |
| 06 | Web app | Image input | Add images in the input, converted to base64. | **Done 2026-11-16.** Up to 4 images per question in the web (button or paste) and the CLI (`--image`, `/image`), base64 over the API, `DataContent` to the agent; images go with their question only ([chat service](feature-llm-chat.md#images)). |
| 07 | Web app | Streaming | Stream the response so the user sees it being generated. | **Done 2026-11-16.** `POST /api/chat/stream` (Server-Sent Events from the agent's `RunStreamingAsync`); the web app renders the pieces with a Stop button, and the CLI prints them as they arrive ([API](../design/api.md#post-apichatstream)). |
| 08 | Extra features | Model selector | Let the user change the model. | **Done 2026-11-16.** `GET /api/models` (the proxy's chat models, cached); a `model` per request, checked against that list; a selector in the web (remembered), `models` / `--model` / `/model` in the CLI ([API](../design/api.md#get-apimodels)). |
| 09 | Extra features | Image generation | Generate images with a gpt-image model. | **Done 2026-11-16.** `POST /api/images` with `Llm:ImageModel` (`gpt-image-2`) through the proxy's images route; an *Images* page in the web (gallery, download) and `dotnextai image` in the CLI ([image generation](feature-images.md)). |
| 10 | Extra features | Token metrics | Consumed tokens per call and in total for the conversation. | **Done 2026-11-16.** The proxy's usage for every answer and image; conversation total = sum of the calls (history resends included); web, CLI (`Tokens:` line, `/usage`) and API (`usage`) ([token metrics](feature-token-metrics.md)). |

## Models available through the proxy

Model list reported by the ERNI LiteLLM proxy for our key (`GET /models`) on 2026-11-17. Each entry has a `mode` field. The model selector (step 08, done) reads the list at runtime through `GET /api/models` and offers only `mode = chat` models, so nothing is hard-coded.

| Model | Mode | Limits (input / output tokens) | Used for |
| ----- | ---- | ------------------------------ | -------- |
| `gpt-6-luna` | chat | 922,000 / 128,000 | Default model (`Llm:DefaultModel` in the local user-secrets). |
| `gpt-6.1-terra` | chat | 922,000 / 128,000 | Option in the model selector. |
| `gpt-6.1-sol` | chat | 922,000 / 128,000 | Option in the model selector. |
| `gpt-6-astra` | chat | 922,000 / 128,000 | Option in the model selector. |
| `gpt-image-2` | image_generation | — | Image generation (step 09). |
| `gpt-realtime-1.5` | realtime | 32,000 / 4,096 | Not used. |
| `text-embedding-3-small` | embedding | 8,192 / — | Not used. |

**History of the access (probes with our key):**

- 2026-11-16: the key's team only had `gpt-5.6-luna` (then the default) and `gpt-5.6-terra`, which had **no healthy deployments**; the `gpt-6.x` models answered 403 `team_model_access_denied`.
- 2026-11-17: the proxy administrator gave the team access to the four `gpt-6.x` chat models; each answered a test question. `gpt-5.6-*` left the key's list (and still has no healthy deployments). `Llm:DefaultModel` is now `gpt-6-luna`. Confirmed with it: a real answer, the history across two turns of `dotnextai chat`, and image input (`dotnextai ask -i`, a red PNG described as "Red"); `/v2/model/info` reports `supports_vision: true` for the `gpt-6.x` models.

## Global rules

- **LLM through LiteLLM only.** Endpoint, key and default model come from configuration (`Llm` section). No code calls a provider directly or hard-codes those values.
- **The key never leaves the backend.** It is never sent to the browser, returned by the API, logged or committed. The web app talks only to DotNextAI's API.
- **Saved chats live in the local database.** Since 2026-11-16 the web app's conversations are saved in SQLite through the API (replacing "no persistence for now"); the CLI's live in its process. Anything else stored is a decision to record here first. Saved content is never logged.
- **No personal data in logs.** Prompts, images and answers are not logged above `Debug`.
- **Testable without the proxy.** Every feature can be tested with a fake model client; CI has no LLM secrets.
- **One implementation, two front ends.** Features live in Application and Infrastructure behind interfaces (`IChatService`…); the CLI and the API (and through it the web app) only adapt input and output.

## Verification

Each step's spec names its tests. Global rules covered today:

- Key not exposed: `HealthEndpointTests.GetHealth_NeverExposesLlmSecrets`.
- Endpoint must be HTTPS (HTTP only on loopback): `LlmOptionsTests`.
- Key never in error messages: `AgentChatServiceTests.AskAsync_ProxyErrorEchoingTheKey_MasksIt` and `AskAsync_Unauthorized_NeverQuotesTheProxyBody`.
- No model call for invalid or unconfigured input: `AgentChatServiceTests.AskAsync_NotConfigured_*`, `AskAsync_EmptyQuestion_*`.
- Saved chats: `SqliteConversationStoreTests`, `ConversationServiceTests`, `ConversationEndpointTests` ([saved chats](feature-saved-chats.md#verification)).
