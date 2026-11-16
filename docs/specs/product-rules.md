# Product rules

> **Status:** Active. Defined 2026-11-16.

DotNextAI is the ERNI course exercise *Project 1: Basic LLM Chatbot App*: build an LLM chatbot step by step, starting with a terminal script, then turn it into a local web app. The code is pushed to a personal GitHub repository and the link is shared in the course chat. The course allows any backend stack.

## Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| Backend | .NET 10 + Microsoft Agent Framework instead of the course's suggested Python + LangChain + FastAPI. | 2026-11-16 |
| LLM access | Only through ERNI's LiteLLM proxy (OpenAI-compatible API), with a URL and a virtual key. | 2026-11-16 |
| Web app | Next.js + React + Fluent UI v9 (the course suggests Streamlit or React). | 2026-11-16 |
| UI languages | Multi-language UI. Languages are still to be confirmed; English and Spanish are proposed. | 2026-11-16 |
| Code language | Code, comments, docs and commit messages in English. | 2026-11-16 |
| Delivery | Small increments, one backlog step (or less) at a time, each with docs and tests. | 2026-11-16 |

## Backlog

The course backlog, with how this repository maps each step. The spec of each step (`feature-*.md`) is written when the step starts.

| # | Phase | Step | Course wording | In DotNextAI |
| -- | ----- | ---- | -------------- | ------------ |
| 01 | Backend basics | Terminal Q&A app | Single question and answer with direct OpenAI API calls (through LiteLLM). | `DotNextAI.Cli` calls the proxy with the OpenAI .NET SDK (`ChatClient`), with no orchestration layer. |
| 02 | Backend basics | Orchestration framework | Call the LLM through LangChain. | Same call through a Microsoft Agent Framework agent (`ChatClientAgent` over `IChatClient`). |
| 03 | Backend basics | Chat history | Add chat history to the conversation. | Multi-turn conversation in the CLI (agent thread / message list). |
| 04 | Backend basics | API | Encapsulate the LLM logic into an API. | `DotNextAI.Api` Minimal API endpoints over the same Infrastructure services. |
| 05 | Web app | Web UI | Streamlit or React, run locally. | `frontend/` Next.js + Fluent UI chat that calls the API. |
| 06 | Web app | Image input | Add images in the input, converted to base64. | Image attachments sent as base64 data URIs (`DataContent`). |
| 07 | Web app | Streaming | Stream the response so the user sees it being generated. | Server-Sent Events from the API, rendered incrementally. |
| 08 | Extra features | Model selector | Let the user change the model. | Model list from the proxy (`/v1/models`), selected per request. |
| 09 | Extra features | Image generation | Generate images with a gpt-image model. | OpenAI .NET SDK `ImageClient` through the proxy. |
| 10 | Extra features | Token metrics | Consumed tokens per call and in total for the conversation. | Usage of each answer (`UsageDetails`), with per-call and conversation totals. |

## Models available through the proxy

Model list reported by the ERNI LiteLLM proxy on 2026-11-16. Each entry has a `mode` field. The model selector (step 08) reads the list at runtime and offers only `mode = chat` models, so nothing is hard-coded.

| Model | Mode | Limits (input / output tokens) | Used for |
| ----- | ---- | ------------------------------ | -------- |
| `gpt-5.6-luna` | chat | 922,000 / 128,000 | Default model (`Llm:DefaultModel` in the local user-secrets), steps 01-08 and 10. |
| `gpt-5.6-terra` | chat | 922,000 / 128,000 | Second option in the model selector (step 08). |
| `gpt-image-2` | image_generation | — | Image generation (step 09). |
| `gpt-realtime-1.5` | realtime | 32,000 / 4,096 | Not used. |
| `text-embedding-3-small` | embedding | 8,192 / — | Not used. |

Image input (step 06) needs a chat model that accepts images. Check this with `gpt-5.6-luna` when step 06 starts.

## Global rules

- **LLM through LiteLLM only.** Endpoint, key and default model come from configuration (`Llm` section). No code calls a provider directly or hard-codes those values.
- **The key never leaves the backend.** It is never sent to the browser, returned by the API, logged or committed. The web app talks only to DotNextAI's API.
- **No persistence for now.** Conversations live in memory, in the CLI process or the browser. Adding storage is a decision to record here first.
- **No personal data in logs.** Prompts, images and answers are not logged above `Debug`.
- **Testable without the proxy.** Every feature can be tested with a fake model client; CI has no LLM secrets.

## Verification

Each step's spec names its tests. Global rules covered today:

- Key not exposed: `HealthEndpointTests.GetHealth_NeverExposesLlmSecrets`.
- Endpoint must be HTTPS (HTTP only on loopback): `LlmOptionsTests`.
