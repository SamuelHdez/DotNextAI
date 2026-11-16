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
- [ ] `frontend-ci.yml` (with step 05).
- [ ] `e2e.yml` with Playwright against the production build and the API using a fake model client (`./scripts/dev.sh e2e`).
- [ ] Branch protection on `main` and secret scanning with push protection in the GitHub repository settings.

## Phase 1 — Backend basics

- [ ] 01 Terminal Q&A: one question and answer from `DotNextAI.Cli` through the proxy (OpenAI .NET SDK).
- [ ] 02 Agent Framework: the same call through a `ChatClientAgent`.
- [ ] 03 Chat history: multi-turn conversation in the CLI.
- [ ] 04 API: chat endpoint in `DotNextAI.Api` over the same services.

## Phase 2 — Web app

- [ ] 05 Web UI: `frontend/` Next.js + Fluent UI v9, multi-language, calling the API.
- [ ] 06 Image input: attachments as base64.
- [ ] 07 Streaming: SSE from the API, incremental rendering.

## Phase 3 — Extra features

- [ ] 08 Model selector.
- [ ] 09 Image generation (gpt-image).
- [ ] 10 Token metrics per call and per conversation.

## Open questions

- [ ] UI languages: confirm the list (English and Spanish proposed) and the default.
- [x] 2026-11-16 — Models of the ERNI LiteLLM proxy: chat `gpt-5.6-luna` (default) and `gpt-5.6-terra`, image `gpt-image-2` (see [product rules](../specs/product-rules.md#models-available-through-the-proxy)).
- [ ] Check that `gpt-5.6-luna` accepts image input (step 06).
