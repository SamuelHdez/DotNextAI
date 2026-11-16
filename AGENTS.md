# AGENTS.md

Instructions for AI coding agents (Claude Code, Codex, Copilot, Cursor…) working on DotNextAI. Humans: start with the [README](README.md) and the [docs index](docs/README.md).

## What this repository is

An LLM chatbot built step by step for the ERNI course *Project 1: Basic LLM Chatbot App*: first a terminal script, then a local web app. The ten backlog steps are listed in [product rules](docs/specs/product-rules.md#backlog) and tracked in [`docs/tasks/todo.md`](docs/tasks/todo.md). Every model call goes through ERNI's LiteLLM proxy (OpenAI-compatible API).

| Path | What | Stack |
| ---- | ---- | ----- |
| `backend/src/DotNextAI.Api/` | HTTP API (port 5100): health, chat (stateless, rate-limited, streaming), models, images, saved chats (`/api/folders`, `/api/conversations`). Contract in [`docs/design/api.md`](docs/design/api.md) | ASP.NET Core 10 Minimal API, OpenAPI + Swagger UI, built-in rate limiter |
| `backend/src/DotNextAI.Application/` | Contracts shared by the API and the CLI: DTOs and the interfaces Infrastructure implements | .NET 10 class library |
| `backend/src/DotNextAI.Infrastructure/` | LLM access: `LlmOptions`, `LlmChatClientFactory` (OpenAI client of the proxy), `AgentChatService` (Agent Framework agent), `ChatFailureMapper` | Microsoft Agent Framework 1.28, Microsoft.Extensions.AI 10.11, OpenAI .NET SDK, `IHttpClientFactory` |
| `backend/src/DotNextAI.Persistence/` | Saved chats: `ChatsDbContext`, EF Core migrations, `SqliteConversationStore` (`IConversationStore`). Spec in [`docs/specs/feature-saved-chats.md`](docs/specs/feature-saved-chats.md) | EF Core 10, SQLite |
| `backend/src/DotNextAI.Cli/` | Terminal app `dotnextai` (**permanent**, grows with the backlog): `chat` (default, with history) and `ask` commands | .NET 10 console, Generic Host, System.CommandLine 2.0 |
| `backend/tests/DotNextAI.UnitTests/` | Unit tests | xUnit v3, Microsoft.Testing.Platform |
| `backend/tests/DotNextAI.IntegrationTests/` | HTTP tests against the in-memory API | xUnit v3, `WebApplicationFactory` |
| `frontend/` | Web app (port 3030): dashboard shell, home, chat with saved chats in folders, images. Spec in [`docs/specs/feature-web-app.md`](docs/specs/feature-web-app.md) | Next.js 16, React 19, TypeScript 5, Fluent UI v9, Vitest |
| `backend/Dockerfile` | API image (non-root, digest-pinned base images); built and scanned in CI, never pushed | Docker |
| `.github/` | CI workflows (`backend-ci.yml`, `security.yml`) and `dependabot.yml` | GitHub Actions |
| `frontend/e2e/` | Playwright suite and the fake LiteLLM proxy it runs against | Playwright, axe-core |
| `scripts/dev.sh` | Local orchestrator | bash (Git Bash, WSL, macOS, Linux) |
| `docs/` | Specs, design and tasks (see below) | Markdown |

The only state is the saved chats database (SQLite, `backend/src/DotNextAI.Api/App_Data/dotnextai.db`, git-ignored), used by the API for the web app. The CLI keeps its conversations in memory.

## Running and checking

```bash
./scripts/dev.sh secrets      # once: LiteLLM endpoint, default model and API key (user-secrets)
./scripts/dev.sh              # API on http://localhost:5100 + web app on http://localhost:3030
./scripts/dev.sh cli           # terminal app: chat with history (`cli ask "…"` for one question)
./scripts/dev.sh test         # as in CI: .NET build + tests, then ESLint, type-check, Vitest (`test frontend` for one part)
./scripts/dev.sh --help       # everything else (api --watch, build, status)
```

Before saying a change is done, run what it touches and report the real result. If something could not run, say so:

- .NET: `./scripts/dev.sh test`, which runs `dotnet build backend/DotNextAI.slnx -warnaserror` and `dotnet test --solution backend/DotNextAI.slnx`. The root `global.json` selects Microsoft.Testing.Platform, so `dotnet test` needs `--solution` or `--project`. It is found by walking up from the current directory, so run .NET commands from the repository or any folder below it. CI (`backend-ci.yml`) restores in locked mode, builds with analyzer warnings as errors, runs Semgrep and scans the API image with Trivy.
- Web: `npm --prefix frontend run lint`, `typecheck`, `test` and `build` (or `./scripts/dev.sh test frontend`). CI also runs `npm audit` and Semgrep.
- UI or API changes: `./scripts/dev.sh e2e` (Playwright + axe against a fake LiteLLM, the real API and the production web build).
- Manual check: `./scripts/dev.sh status` while `./scripts/dev.sh` runs.

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
- Model failures surface as `ChatServiceException` with a `ChatFailure`. Map new HTTP statuses in `ChatFailureMapper.FromStatus`. Build OpenAI clients only through `LlmChatClientFactory`, so endpoint, key, timeout, retries and the test transport stay in one place. Only quote the proxy's error body through `ProxyErrorDetail`, which keeps the first line and masks the key, and never for 401/403.

Backend:

- Agent Framework and `Microsoft.Extensions.AI` must stay on the 10.x line of `Microsoft.Extensions.AI`. Before updating Agent Framework, check its `.nuspec` dependencies; a version that needs `Microsoft.Extensions.AI` 11.x waits for the move to .NET 11.

- Layering: Api → Application ← Infrastructure, Api → Persistence → Application, and Cli → Application + Infrastructure. Application references no other project and no SDKs. Agent Framework, OpenAI and HTTP details stay in Infrastructure; EF Core and SQLite in Persistence.
- Endpoints are Minimal APIs grouped in `DotNextAI.Api/Endpoints/*Endpoints.cs`. They return `TypedResults` and errors as `ProblemDetails`. HTTP DTOs live in `DotNextAI.Api/Contracts`.
- Model failures become problems through `ChatProblems`: status per `ChatFailure` and a `failure` extension that the web app translates. Never forward the proxy's 401/403 as 401/403 (the key is the server's). Keep `docs/design/api.md` in sync with every endpoint, field, status code and `failure` value.
- Streaming endpoints await the first update before returning `TypedResults.ServerSentEvents`, so failures before any text keep their ProblemDetails and status. Later failures become an `error` event with the same `failure` code (`ChatStreamEvents`). `IChatService` streams are iterated once; C# cannot `yield` inside a `try` with a `catch`, so only `MoveNextAsync` is guarded.
- Every endpoint that spends the LiteLLM key needs `RequireRateLimiting(...)`. Validate input with `ChatRules` before calling the model and answer 400 `ValidationProblem` with errors per field.
- Token metrics come only from the provider's counters (`TokenUsage`, null when not reported). Never estimate them or show zeros for unknown usage. A conversation total is the sum of its calls.
- Image generation goes through `IImageService` and `LiteLlmHttp` (no SDK image client, no retries, its own rate limit). Never log prompts or image bytes.
- Models: callers pick a model only from `IModelCatalog` (the proxy's `mode = chat` list); the API answers 400 `errors.model` and the service throws `ModelNotFound` for anything else. Never pass a client's model name to the proxy unchecked. New calls go through `ChatRequest`.
- Images (`ChatImage`): the format comes from the bytes (`ImageFormats.Detect`), never from a name or a declared type. Rules live in `ChatRules.TryValidateImages` and are mirrored in `frontend/src/lib/chat/images.ts`. Images go only with their question; the history stays text. Never log image bytes.
- `IChatService` is stateless: callers pass the history with each question. The CLI keeps it in memory (`ChatConversation`); for saved chats `ConversationService` reads it from `IConversationStore`. Do not store anything else on the server without recording the decision in the specs first.
- Saved chats (`DotNextAI.Persistence`): the layering is `Api → Persistence → Application`; endpoints and `ConversationService` use `IConversationStore`, never the `DbContext`. Every model change needs a migration (`dotnet tool run dotnet-ef migrations add <Name> --project backend/src/DotNextAI.Persistence --output-dir Migrations`); CI fails on pending model changes. Lists never load image bytes. Never log saved content. An exchange is saved only when the answer completes or is stopped, never when it fails.
- Configuration goes through options classes in `DotNextAI.Infrastructure/Options` with `ValidateDataAnnotations` and `ValidateOnStart`. A missing LLM configuration must not stop the API from starting. It reports `LlmConfigured = false`.
- Analyzers (`latest-recommended`, security rules `All`) run on every build, and CI treats warnings as errors. Fix warnings rather than suppressing them. When a suppression is justified, scope it and explain it in a comment.

Terminal app (`DotNextAI.Cli`):

- The CLI is a permanent deliverable. A backend change must keep it building, tested and documented, and new features are added to it where they make sense ([CLI spec](docs/specs/feature-cli.md#roadmap)).
- Commands are classes in `DotNextAI.Cli/Commands` wired in `CliApplication`. They take `IChatService`-style interfaces and `CliConsole`, never `Console` directly, so `CliApplicationTests` can drive them.
- stdout carries only answers; prompts, warnings and errors go to stderr. Keep the exit codes of `ExitCodes` (0, 1, 2, 3, 130) and document new ones in the spec.
- The CLI's texts are English (invariant culture). Chat commands start with `/` and are never sent to the model.

Web app (`frontend/`):

- **No hard-coded UI text.** Every string is a key in `src/lib/i18n/messages/en.ts` with its Spanish text in `es.ts` (same keys and `{placeholders}`, enforced by the compiler and `messages.test.ts`). Use `useI18n().t(...)` in client components and `translate(await getRequestLocale(), ...)` in server code (metadata).
- Error texts come from the API's `failure` code (`chatErrorMessage`, chat and saved chats failures), never from `detail`.
- Saved chats: the chat page is `ChatWorkspace` (panel + `ChatView`); calls go through `src/services/conversationsService.ts`, and `src/lib/chat/savedChats.ts` mirrors the backend's `ConversationRules`. Fluent dialogs close with `open={false}` and stay mounted until closed: unmounting an open one leaves the page `aria-hidden`.
- Keep `src/services/types.ts` in sync with `docs/design/api.md`, `src/lib/chat/chatRules.ts` with the backend's `ChatRules`, and `src/lib/backlog.ts` with the backlog in `docs/specs/product-rules.md`.
- Styling only with Fluent `makeStyles` and `tokens` (ERNI theme in `src/lib/theme`), no hard-coded colours. Pages use `PageBody` / `PageHeader`; new pages get a link in `SideNav` (`NAV_GROUPS`).
- Keep the CSP strict: no new inline scripts without the nonce, and any new origin the browser must reach goes into `buildContentSecurityPolicy`.
- npm only. New or updated packages must be at least 7 days old (`npm install --before=<date>`), installed with `--ignore-scripts`, and the `package-lock.json` committed.
- Tests: Vitest + Testing Library next to the code (`*.test.ts(x)`), rendered with `renderWithProviders`, and `fetch` stubbed (never a real API).
- E2E in `frontend/e2e` ([testing](docs/design/testing.md)). Locate by role and accessible name, like users do. New pages and states get a scenario and an `expectNoA11yViolations` check, with zero violations allowed. New model behaviours are added to `e2e/fake-litellm.mjs`, never as test code in the API.

Tests and CI:

- Test names: `Method_Scenario_ExpectedResult`. Use `TestContext.Current.CancellationToken` in async calls.
- The integration tests run the API in the `Testing` environment (`DotNextAIApiFactory`), so the developer's user-secrets never reach them. Each client gets its own temporary SQLite file; unit tests of the store use in-memory SQLite with the real migrations (`SqliteTestDatabase`), not the EF in-memory provider.
- Semgrep (`p/secrets`) will block CI on literal keys or tokens. Generate them at runtime in tests (`$"sk-{Guid.NewGuid():N}"`).
- Workflows (`.github/workflows/`) mirror the ValidatorHub and ctf-janus pipelines: actions pinned by commit SHA, tool binaries pinned by version and checksum (Gitleaks, Trivy), `permissions: contents: read`, `persist-credentials: false`, no secrets, and zizmor checks them (`security.yml`). Before pushing a workflow change, run `uvx --from zizmor zizmor .github/`.
- The API image (`backend/Dockerfile`, build context `backend/`) copies each project's `.csproj` and `packages.lock.json` before the restore: add new `src/` projects there too. Base images are pinned by digest.

Repository hygiene:

- After changing NuGet packages, run `dotnet restore backend/DotNextAI.slnx` and commit the `packages.lock.json` files.
- Stay on the .NET 10 (LTS) line for ASP.NET Core and `Microsoft.Extensions.*` packages. Do not mix in 11.x versions.
- Text files are stored with LF (`.gitattributes`); `*.sh` must stay LF.
- Secrets live in user-secrets (`./scripts/dev.sh secrets`), never in tracked files. `.env*` and `appsettings.*.local.json` are git-ignored.
- npm only (no pnpm/yarn). `node_modules` installed from Windows does not work in WSL and vice versa; `frontend/.env.local` is generated by `dev.sh` and git-ignored.

Language:

- Code, comments, docs and commit messages in English.
- The UI is multi-language. Use translation keys, never hard-coded UI strings. The language list is in [product rules](docs/specs/product-rules.md).
- Commit messages: imperative subject (Conventional Commits prefix such as `feat:`, `docs:`, `ci:`), and a body that explains why. No secrets.
