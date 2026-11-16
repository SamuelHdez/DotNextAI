# DotNextAI Documentation

Central index for the specs, design and day-to-day tasks of DotNextAI. Setup and run instructions live in the [root README](../README.md); rules for AI agents in [AGENTS.md](../AGENTS.md).

## Specs — scope and features

| Document | Description | Status |
| -------- | ----------- | ------ |
| [Product rules](./specs/product-rules.md) | Course goal, the ten backlog steps, the models of the proxy, and the global rules (LLM through LiteLLM only, secrets, languages, no persistence). | Active |

| [Terminal app (CLI)](./specs/feature-cli.md) | `dotnextai`: chat with history and `ask`, images, input modes, chat commands, messages, exit codes, and the plan for the CLI in later steps. Permanent deliverable. | Step 08 implemented |
| [Web app](./specs/feature-web-app.md) | `frontend/`: dashboard shell, ERNI theme, English/Spanish, home and chat pages, image attachments, streamed answers with Stop, model selector, errors, limits, security. | Step 08 implemented |
| [LLM chat service](./specs/feature-llm-chat.md) | `IChatService` contract shared by the CLI and the API, the Agent Framework agent over the LiteLLM proxy, rules, images, streaming, model catalog, failure mapping, configuration. | Step 08 implemented |

One `feature-*.md` per backlog step or feature is added when it starts.

## Design — architecture and decisions

| Document | Description | Status |
| -------- | ----------- | ------ |
| [Architecture](./design/architecture.md) | Components, ports, configuration, project layering, and the technology decisions log. | Step 08 implemented |
| [Testing](./design/testing.md) | Test layers (.NET unit and integration, Vitest, Playwright E2E with axe), the fake LiteLLM proxy and how to run each. | Implemented |
| [API contract](./design/api.md) | Endpoints, request and response JSON, validation, status codes and the `failure` extension, models, images, streaming (SSE), rate limit, CORS. Source for the web app's types. | Step 08 implemented |

## Tasks — day-to-day work

| Document | Description |
| -------- | ----------- |
| [To-do / backlog](./tasks/todo.md) | Course steps and repository work, pending and done. |
| [Agent prompts](./tasks/agent-prompts.md) | History of the prompts that shaped the project. |
