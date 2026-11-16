# AGENTS.md

Instructions for AI coding agents (Claude Code, Codex, Copilot, Cursor…) working on DotNextAI. Humans: start with the [README](README.md) and the [docs index](docs/README.md).

## What this repository is

An LLM chatbot built step by step for the ERNI course *Project 1: Basic LLM Chatbot App*: first a terminal script, then a local web app. The ten backlog steps are listed in [product rules](docs/specs/product-rules.md#backlog) and tracked in [`docs/tasks/todo.md`](docs/tasks/todo.md). Every model call goes through ERNI's LiteLLM proxy (OpenAI-compatible API).

| Path | What | Stack |
| ---- | ---- | ----- |
| `backend/src/DotNextAI.Api/` | HTTP API (port 5100): health today; chat, streaming, models and images in later steps | ASP.NET Core 10 Minimal API, OpenAPI + Swagger UI |
| `backend/src/DotNextAI.Application/` | Contracts shared by the API and the CLI: DTOs and the interfaces Infrastructure implements | .NET 10 class library |
| `backend/src/DotNextAI.Infrastructure/` | LLM access: `LlmOptions`, `OpenAiChatService` (step 01); Microsoft Agent Framework agents from step 02 | OpenAI .NET SDK, `IHttpClientFactory`; Microsoft Agent Framework, Microsoft.Extensions.AI |
| `backend/src/DotNextAI.Cli/` | Terminal app `dotnextai` (**permanent**, grows with the backlog): `ask` command, interactive mode | .NET 10 console, Generic Host, System.CommandLine 2.0 |
| `backend/tests/DotNextAI.UnitTests/` | Unit tests | xUnit v3, Microsoft.Testing.Platform |
| `backend/tests/DotNextAI.IntegrationTests/` | HTTP tests against the in-memory API | xUnit v3, `WebApplicationFactory` |
| `frontend/` | *(Not created yet)* Web app (port 3030) | Next.js, React, Fluent UI v9, multi-language |
| `backend/Dockerfile` | API image (non-root, digest-pinned base images); built and scanned in CI, never pushed | Docker |
| `.github/` | CI workflows (`backend-ci.yml`, `security.yml`) and `dependabot.yml` | GitHub Actions |
| `scripts/dev.sh` | Local orchestrator | bash (Git Bash, WSL, macOS, Linux) |
| `docs/` | Specs, design and tasks (see below) | Markdown |

There is no database: the backend keeps no state yet.

## Running and checking

```bash
./scripts/dev.sh secrets      # once: LiteLLM endpoint, default model and API key (user-secrets)
./scripts/dev.sh              # API on http://localhost:5100 (Swagger UI at /swagger)
./scripts/dev.sh cli ask "…"   # terminal app (or `cli` alone for interactive mode)
./scripts/dev.sh test         # restore + Release build with warnings as errors + unit and integration tests
./scripts/dev.sh --help       # everything else (api --watch, build, status)
```

Before saying a change is done, run what it touches and report the real result. If something could not run, say so:

- .NET: `./scripts/dev.sh test`, which runs `dotnet build backend/DotNextAI.slnx -warnaserror` and `dotnet test --solution backend/DotNextAI.slnx`. The root `global.json` selects Microsoft.Testing.Platform, so `dotnet test` needs `--solution` or `--project`. It is found by walking up from the current directory, so run .NET commands from the repository or any folder below it. CI (`backend-ci.yml`) restores in locked mode, builds with analyzer warnings as errors, runs Semgrep and scans the API image with Trivy.
- Manual check of the API: `./scripts/dev.sh status` while `./scripts/dev.sh` runs.

## Documentation is part of every change

Nothing is finished until the docs say what the code does. Use the existing structure in `docs/` and do not create documents outside it (except this file and READMEs):

| Folder | Holds | When to touch it |
| ------ | ----- | ---------------- |
| `docs/specs/` | `product-rules.md` (scope, backlog, global rules) and one `feature-*.md` per backlog step or feature: behavior, rules, UI texts, API, verification | **Before** changing a behavior: update the spec, then the code. A new feature gets a new `feature-<name>.md`. |
| `docs/design/` | Architecture, API contract and technology decisions | When components, endpoints, configuration or technology decisions change. |
| `docs/tasks/` | `todo.md` (backlog: `- [ ]` pending, `- [x] YYYY-MM-DD — …` done) and `agent-prompts.md` (prompt history) | Mark finished work with the date; add follow-ups you leave open. |
| `docs/README.md` | Index of every document with a one-line description and status | Whenever a document is added, renamed or changes status. |
| `README.md` | Setup, commands, configuration, security notes, verification | When commands, settings, ports or requirements change. |

Conventions for documents:

- English, Markdown, relative links.
- Specs start with a title and a `> **Status:** …` line (date when implemented), then use these sections: decisions table (topic, decision, date), rules, UI/API details, and a **Verification** section naming the tests that cover it.
- Record decisions with their date and reason. When a decision is reversed, update every document that stated the old one and say what replaced it.
- Keep the same facts consistent everywhere they appear (README, specs, design, `todo.md`). Search for the old wording before finishing.

## Rules for code

LLM and security:

- Every model call goes through the LiteLLM proxy configured in `Llm` (`LlmOptions`). Never call a provider directly or hard-code endpoints, keys or model names in code.
- The API key never leaves the backend. Keep it out of API responses, the frontend bundle, logs, exceptions and test output. `GetHealth_NeverExposesLlmSecrets` guards the health endpoint; add the same check to new endpoints that echo configuration.
- Tests never call the real proxy. Use a fake `IChatClient` (Microsoft.Extensions.AI) or a stub `HttpMessageHandler`. CI has no LLM secrets.
- Do not log prompts, images or model answers above `Debug`. They may contain personal data.
- Model failures surface as `ChatServiceException` with a `ChatFailure`. Map new HTTP statuses in `OpenAiChatService.MapFailure`. Only quote the proxy's error body through `ProxyErrorDetail`, which keeps the first line and masks the key, and never for 401/403.

Backend:

- Layering: Api → Application ← Infrastructure, and Cli → Application + Infrastructure. Application references no other project and no SDKs. Agent Framework, OpenAI and HTTP details stay in Infrastructure.
- Endpoints are Minimal APIs grouped in `DotNextAI.Api/Endpoints/*Endpoints.cs`. They return `TypedResults` and errors as `ProblemDetails`.
- Configuration goes through options classes in `DotNextAI.Infrastructure/Options` with `ValidateDataAnnotations` and `ValidateOnStart`. A missing LLM configuration must not stop the API from starting. It reports `LlmConfigured = false`.
- Analyzers (`latest-recommended`, security rules `All`) run on every build, and CI treats warnings as errors. Fix warnings rather than suppressing them. When a suppression is justified, scope it and explain it in a comment.

Terminal app (`DotNextAI.Cli`):

- The CLI is a permanent deliverable. A backend change must keep it building, tested and documented, and new features are added to it where they make sense ([CLI spec](docs/specs/feature-cli.md#roadmap)).
- Commands are classes in `DotNextAI.Cli/Commands` wired in `CliApplication`. They take `IChatService`-style interfaces and `CliConsole`, never `Console` directly, so `CliApplicationTests` can drive them.
- stdout carries only answers; prompts, warnings and errors go to stderr. Keep the exit codes of `ExitCodes` (0, 1, 2, 3, 130) and document new ones in the spec.
- The CLI's texts are English (invariant culture).

Tests and CI:

- Test names: `Method_Scenario_ExpectedResult`. Use `TestContext.Current.CancellationToken` in async calls.
- The integration tests run the API in the `Testing` environment (`DotNextAIApiFactory`), so the developer's user-secrets never reach them.
- Semgrep (`p/secrets`) will block CI on literal keys or tokens. Generate them at runtime in tests (`$"sk-{Guid.NewGuid():N}"`).
- Workflows (`.github/workflows/`) mirror the ValidatorHub and ctf-janus pipelines: actions pinned by commit SHA, tool binaries pinned by version and checksum (Gitleaks, Trivy), `permissions: contents: read`, `persist-credentials: false`, no secrets, and zizmor checks them (`security.yml`). Before pushing a workflow change, run `uvx --from zizmor zizmor .github/`.
- The API image (`backend/Dockerfile`, build context `backend/`) copies each project's `.csproj` and `packages.lock.json` before the restore: add new `src/` projects there too. Base images are pinned by digest.

Repository hygiene:

- After changing NuGet packages, run `dotnet restore backend/DotNextAI.slnx` and commit the `packages.lock.json` files.
- Stay on the .NET 10 (LTS) line for ASP.NET Core and `Microsoft.Extensions.*` packages. Do not mix in 11.x versions.
- Text files are stored with LF (`.gitattributes`); `*.sh` must stay LF.
- Secrets live in user-secrets (`./scripts/dev.sh secrets`), never in tracked files. `.env*` and `appsettings.*.local.json` are git-ignored.
- npm only (no pnpm/yarn) once the frontend exists.

Language:

- Code, comments, docs and commit messages in English.
- The UI is multi-language. Use translation keys, never hard-coded UI strings. The language list is in [product rules](docs/specs/product-rules.md).
- Commit messages: imperative subject (Conventional Commits prefix such as `feat:`, `docs:`, `ci:`), and a body that explains why. No secrets.
