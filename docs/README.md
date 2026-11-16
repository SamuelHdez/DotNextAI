# DotNextAI Documentation

Central index for the specs, design and day-to-day tasks of DotNextAI. Setup and run instructions live in the [root README](../README.md); rules for AI agents in [AGENTS.md](../AGENTS.md).

## Specs — scope and features

| Document | Description | Status |
| -------- | ----------- | ------ |
| [Product rules](./specs/product-rules.md) | Course goal, the ten backlog steps, the models of the proxy, and the global rules (LLM through LiteLLM only, secrets, languages, no persistence). | Active |

| [Terminal app (CLI)](./specs/feature-cli.md) | `dotnextai`: chat with history and `ask`, input modes, chat commands, messages, exit codes, and the plan for the CLI in later steps. Permanent deliverable. | Step 03 implemented |
| [LLM chat service](./specs/feature-llm-chat.md) | `IChatService` contract shared by the CLI and the API, the Agent Framework agent over the LiteLLM proxy, rules, failure mapping, configuration. | Step 03 implemented |

One `feature-*.md` per backlog step or feature is added when it starts.

## Design — architecture and decisions

| Document | Description | Status |
| -------- | ----------- | ------ |
| [Architecture](./design/architecture.md) | Components, ports, configuration, project layering, and the technology decisions log. | Step 04 implemented |
| [API contract](./design/api.md) | Endpoints, request and response JSON, validation, status codes and the `failure` extension, rate limit, CORS. Source for the web app's types. | Step 04 implemented |

## Tasks — day-to-day work

| Document | Description |
| -------- | ----------- |
| [To-do / backlog](./tasks/todo.md) | Course steps and repository work, pending and done. |
| [Agent prompts](./tasks/agent-prompts.md) | History of the prompts that shaped the project. |
