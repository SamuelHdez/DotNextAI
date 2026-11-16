# DotNextAI Documentation

Central index for the specs, design and day-to-day tasks of DotNextAI. Setup and run instructions live in the [root README](../README.md); rules for AI agents in [AGENTS.md](../AGENTS.md).

## Specs — scope and features

| Document | Description | Status |
| -------- | ----------- | ------ |
| [Product rules](./specs/product-rules.md) | Course goal, the ten backlog steps, the models of the proxy, and the global rules (LLM through LiteLLM only, secrets, languages, saved chats in a local database). | Active |
| [Saved chats](./specs/feature-saved-chats.md) | Conversations and folders saved in a local SQLite database: decisions, rules, data model, when an exchange is saved, verification. | Implemented (backend and web) |
| [Terminal app (CLI)](./specs/feature-cli.md) | `dotnextai`: chat with history and `ask`, images, input modes, chat commands, messages, exit codes, and the plan for the CLI in later steps. Permanent deliverable. | Step 10 implemented |
| [Token metrics](./specs/feature-token-metrics.md) | Tokens per answer, per image and per conversation in the web, the CLI and the API; where they come from and how they are shown. | Step 10 implemented |
| [Image generation](./specs/feature-images.md) | `gpt-image-2` through the proxy: rules, the `/images` page, `dotnextai image`, failures, limits. | Step 09 implemented |
| [Web app](./specs/feature-web-app.md) | `frontend/`: dashboard shell, ERNI theme, English/Spanish, home and chat pages, saved chats panel, image attachments, streamed answers with Stop, model selector, images page, token metrics, errors, limits, security. | Saved chats implemented |
| [LLM chat service](./specs/feature-llm-chat.md) | `IChatService` contract shared by the CLI and the API, the Agent Framework agent over the LiteLLM proxy, rules, images, streaming, model catalog, usage, failure mapping, configuration. | Step 10 implemented |

One `feature-*.md` per backlog step or feature is added when it starts.

## Design — architecture and decisions

| Document | Description | Status |
| -------- | ----------- | ------ |
| [Architecture](./design/architecture.md) | Components, ports, configuration, project layering, and the technology decisions log. | Saved chats part 1 implemented |
| [Testing](./design/testing.md) | Test layers (.NET unit and integration, Vitest, Playwright E2E with axe), the fake LiteLLM proxy, how to run each, and a known jsdom issue. | Implemented |
| [API contract](./design/api.md) | Endpoints, request and response JSON, validation, status codes and the `failure` extension, models, image input, streaming (SSE), image generation, token usage, saved chats, rate limits, CORS. Source for the web app's types. | Saved chats part 1 implemented |

## Tasks — day-to-day work

| Document | Description |
| -------- | ----------- |
| [To-do / backlog](./tasks/todo.md) | Course steps and repository work, pending and done. |
| [Agent prompts](./tasks/agent-prompts.md) | History of the prompts that shaped the project. |
