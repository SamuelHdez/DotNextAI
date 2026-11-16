# DotNextAI

An LLM chatbot built step by step for the ERNI course *Project 1: Basic LLM Chatbot App*. It starts as a terminal script and becomes a local web app. The backend is .NET 10 with [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/), every model call goes through ERNI's LiteLLM proxy, and the web app (a later step) is Next.js with Fluent UI.

Technical documentation, backlog and decisions: [docs/](docs/README.md).

> **Status:** backlog step 05 done. A web app (Next.js + Fluent UI, English/Spanish) and the terminal app chat through the same API and Microsoft Agent Framework agent over the LiteLLM proxy.

## Requirements

- .NET SDK 10 (LTS).
- Git Bash on Windows (it comes with Git for Windows), or WSL, macOS or Linux, to run `scripts/dev.sh`.
- A LiteLLM proxy URL and virtual key from ERNI, plus the name of a model it exposes.
- Node.js 24 LTS and npm (22 or later works locally), for the web app.
- Optional: Docker, to build the images locally (`docker build backend`, `docker build --build-arg NEXT_PUBLIC_API_BASE_URL=http://localhost:5100 frontend`). CI builds both.
- Later steps: Playwright browsers (E2E).

## Running the project

From the repository root, in Git Bash:

```bash
./scripts/dev.sh secrets      # once: LiteLLM endpoint, default model and API key
./scripts/dev.sh              # API (http://localhost:5100) + web app (http://localhost:3030); Ctrl+C stops both
./scripts/dev.sh api --watch  # only the API, under dotnet watch (Swagger UI on /swagger)
./scripts/dev.sh web          # only the web app
./scripts/dev.sh cli ask "What is .NET?"   # terminal app: one question, one answer
./scripts/dev.sh cli          # terminal app: chat with history
./scripts/dev.sh test         # as in CI: .NET build + tests, then ESLint, type-check and Vitest (`test backend|frontend`)
./scripts/dev.sh build        # .NET Release build + Next.js production build (`build backend|frontend`)
./scripts/dev.sh status       # SDK, LLM settings (never the key) and whether the API answers
./scripts/dev.sh --help
```

Without the script:

```powershell
dotnet run --project backend/src/DotNextAI.Api --launch-profile http
dotnet run --project backend/src/DotNextAI.Cli
dotnet test --solution backend/DotNextAI.slnx
npm --prefix frontend ci --ignore-scripts && npm --prefix frontend run dev   # needs frontend/.env.local, see .env.example
```

`node_modules` is OS-specific: dependencies installed from Windows do not work in WSL and vice versa. From WSL, `dev.sh` uses the Windows .NET SDK and Node.js when no Linux SDK is installed.

## Configuration

The settings live in the `Llm` section. The API and the CLI share one `dotnet user-secrets` store (`UserSecretsId` `b20f238b-…`), so `./scripts/dev.sh secrets` configures both. The script sends the values through stdin, so the key never appears in a command line.

| Setting | Meaning | Where |
| ------- | ------- | ----- |
| `Llm:Endpoint` | Base URL of the LiteLLM proxy. HTTPS (HTTP only on `localhost`) | user-secrets |
| `Llm:ApiKey` | Proxy virtual key (**secret**) | user-secrets |
| `Llm:DefaultModel` | Model name as exposed by the proxy | user-secrets |
| `Llm:TimeoutSeconds` | Timeout of each model call attempt, 5-600 (default 120) | `appsettings.json` |
| `Llm:MaxRetries` | Retries on 408/429/5xx and network errors, 0-5 (default 2) | `appsettings.json` |
| `RateLimiting:ChatPermitsPerMinute` | Requests to `/api/chat` per client IP and minute, 1-10,000 (default 20) | `appsettings.json` |
| `Cors:AllowedOrigins` | Browser origins allowed to call the API (`http://localhost:3030` in Development) | `appsettings*.json` |

Environment variables override both, using the `Llm__ApiKey` form. While the three `Llm` values are missing, the API still starts and `/api/health` reports `"llmConfigured": false`. The CLI exits with a hint.

To read or reset the store: `dotnet user-secrets list --project backend/src/DotNextAI.Api` (or `clear`). Windows and WSL keep separate stores.

## Web app

Open `http://localhost:3030` after `./scripts/dev.sh`. A dashboard with the ValidatorHub layout and the ERNI brand:

- **Home**: service status (API, language model) and the course backlog.
- **Chat**: the conversation stays in the browser tab and is sent with every question. Enter sends, Shift+Enter adds a line, and **New conversation** starts over.
- **Header**: language selector (English / Español, remembered in a cookie) and appearance (system / light / dark).

Details in the [web app spec](docs/specs/feature-web-app.md).

## Terminal app

`dotnextai` is a permanent part of the project: it keeps working, and grows with the backlog, next to the web app. Full behavior in the [CLI spec](docs/specs/feature-cli.md).

```bash
dotnextai                            # chat with history (/new, /help, /exit; Ctrl+D to quit)
dotnextai chat < questions.txt       # one question per line, answers only on stdout
dotnextai ask "What is .NET?"        # one question, no history (quotes optional)
echo "Summarize: ..." | dotnextai ask  # one multi-line question from stdin
dotnextai --help
```

Answers go to stdout and everything else to stderr, so answers can be piped. Exit codes: `0` answer or chat ended, `1` model error, `2` usage error, `3` not configured, `130` Ctrl+C. Run it with `./scripts/dev.sh cli …` or `dotnet run --project backend/src/DotNextAI.Cli -- …`.

## API

```bash
curl -s -H "Content-Type: application/json" \
  -d '{"question":"What is my name?","history":[{"role":"user","text":"My name is Ana."},{"role":"assistant","text":"Hi Ana!"}]}' \
  http://localhost:5100/api/chat
# {"answer":"Your name is Ana.","model":"gpt-5.6-luna","truncated":false}
```

The API is stateless: the client sends the history with every question. Errors are `ProblemDetails` with a `failure` code (`ModelUnavailable`, `Timeout`, …). See the [API contract](docs/design/api.md), or use `backend/src/DotNextAI.Api/DotNextAI.Api.http` and Swagger UI.

## Project layout

```text
global.json  SDK line and Microsoft.Testing.Platform (applies to the whole repository)
backend/
  DotNextAI.slnx  Directory.Build.props
  src/DotNextAI.Api             Minimal API (port 5100): /api/health, /api/chat
  src/DotNextAI.Application     DTOs and interfaces
  src/DotNextAI.Infrastructure  LLM options and clients (Agent Framework)
  src/DotNextAI.Cli             terminal app (dotnextai)
  tests/DotNextAI.UnitTests  tests/DotNextAI.IntegrationTests
frontend/   Next.js + Fluent UI web app (port 3030): src/app, components, lib (i18n, theme, chat), services
docs/       specs, design, tasks
scripts/    dev.sh
```

See [architecture](docs/design/architecture.md) for the components and the decisions behind them.

## Security notes

- The LiteLLM key stays in the backend: it is not in API responses, error messages, logs or tracked files. Tests check `/api/health` and the error messages (the key is masked even if the proxy echoes it).
- Prompts and answers are never logged; logs hold model, duration and status only.
- The web app sends a strict, nonce-based Content-Security-Policy; the browser may only connect to the app and the API.
- The build runs the .NET security analyzers and fails on vulnerable NuGet packages (`NuGetAudit`). Restores are reproducible from `packages.lock.json`.
- The API accepts browser calls only from the origins in `Cors:AllowedOrigins`.
- The API has no authentication and is meant for local use; `/api/chat` is rate-limited per IP because every call spends the LiteLLM key. Add authentication before exposing it anywhere else.
- Tests never call the real proxy and need no secrets.

## CI

GitHub Actions, the same pipelines as ValidatorHub and ctf-janus:

| Workflow | Runs on | What it does |
| -------- | ------- | ------------ |
| `backend-ci.yml` | Changes to `backend/`, `global.json`, `nuget.config` | Semgrep (C#, Dockerfile, secrets) → locked restore → vulnerable-package check → Release build with `-warnaserror` → unit and integration tests with coverage → API image build, CycloneDX SBOM and Trivy scan (fails on fixable HIGH/CRITICAL) |
| `frontend-ci.yml` | Changes to `frontend/` | Semgrep (TypeScript, React, Next.js, Dockerfile, secrets) → `npm ci --ignore-scripts` → registry signatures and `npm audit` → ESLint → type-check → Vitest with coverage → production build → web image build, SBOM and Trivy scan |
| `security.yml` | Every push and PR to `main`, plus weekly | Gitleaks over the whole git history, and zizmor over the workflows |
| `dependabot.yml` | Weekly | npm, NuGet, Docker base images and GitHub Actions, each update 7 days after its release. Framework majors (.NET 11, Node, TypeScript, ESLint) are done manually |

All actions are pinned by commit SHA, tool binaries by version and checksum, and the workflows use `permissions: contents: read` and need no secrets.

## Verification

```bash
./scripts/dev.sh test
```

Expected: .NET build with 0 warnings, ESLint and type-check clean, all tests green (frontend: Vitest suites; backend: `LlmOptionsTests`, `ChatRulesTests`, `ChatConversationTests`, `AgentChatServiceTests`, `CliApplicationTests`, `ChatCommandTests`, `HealthEndpointTests`, `ChatEndpointTests`). End to end, against the proxy: `./scripts/dev.sh cli ask "Reply with exactly: pong"`.
