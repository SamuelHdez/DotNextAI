# Architecture

> **Status:** Step 00 implemented on 2026-11-16 (backend skeleton, health endpoint, CLI shell, backend CI). Components marked *planned* arrive with later backlog steps.

## Components

```mermaid
flowchart LR
    user([User])
    cli["DotNextAI.Cli<br/>terminal chatbot"]
    web["frontend<br/>Next.js + Fluent UI<br/>:3030 (planned)"]
    api["DotNextAI.Api<br/>ASP.NET Core Minimal API<br/>:5100"]
    infra["DotNextAI.Infrastructure<br/>Agent Framework + OpenAI SDK"]
    litellm[("ERNI LiteLLM proxy<br/>OpenAI-compatible")]

    user --> cli
    user --> web
    web -- "HTTP / SSE (CORS)" --> api
    cli --> infra
    api --> infra
    infra -- "HTTPS + virtual key" --> litellm
```

| Component | Responsibility |
| --------- | -------------- |
| `DotNextAI.Api` | HTTP surface. Today: `GET /api/health`. Planned: chat, streaming (SSE), models, image generation. OpenAPI document at `/openapi/v1.json` and Swagger UI at `/swagger` in Development. |
| `DotNextAI.Application` | Contracts with no dependencies: DTOs (`HealthStatusDto`) and the interfaces that Infrastructure implements (chat, images; planned). |
| `DotNextAI.Infrastructure` | Configuration (`LlmOptions`) and, from step 01, the model clients: OpenAI .NET SDK pointed at the proxy, wrapped as `IChatClient` and Agent Framework agents. |
| `DotNextAI.Cli` | Terminal chatbot (steps 01-03). It uses the same Infrastructure as the API, so the logic is written once. |
| `frontend` | *Planned (step 05).* Multi-language chat UI that only talks to `DotNextAI.Api`. |

Project references: `Api → Application, Infrastructure`; `Cli → Application, Infrastructure`; `Infrastructure → Application`. Application references nothing.

## Ports

| Service | URL |
| ------- | --- |
| API | `http://localhost:5100` (`launchSettings.json`, profile `http`) |
| Web app (planned) | `http://localhost:3030`, already allowed by CORS in Development |

## API

| Method | Path | Answer |
| ------ | ---- | ------ |
| `GET` | `/api/health` | `200` `{ status, service, environment, llmConfigured, timestampUtc }`. `llmConfigured` is true when endpoint, key and default model are set. It never contains the endpoint or the key. |

Errors use `ProblemDetails` (`AddProblemDetails`, `UseExceptionHandler`, `UseStatusCodePages`).

## Configuration

`LlmOptions` (section `Llm`) is bound with `ValidateDataAnnotations` and `ValidateOnStart`. Missing values do not stop the startup, so the API runs and `llmConfigured` is false. An out-of-range `TimeoutSeconds` does stop it. The secrets live in `dotnet user-secrets`, in one store shared by the API and the CLI (same `UserSecretsId`). The CLI runs in `Development` by default so it loads that store. The integration tests use the `Testing` environment so they never load it. See the [README](../../README.md#configuration) for the setting list.

## Build and quality

- `backend/Directory.Build.props`: `net10.0`, nullable, .NET analyzers `latest-recommended` with the security rules set to `All`, `NuGetAudit` (vulnerable packages fail the build), and `packages.lock.json` with locked-mode restore when `ContinuousIntegrationBuild=true`.
- `global.json` (repository root, so it applies from the root and from `backend/`): SDK 10.0.100 or a later feature band; tests run on Microsoft.Testing.Platform (xUnit v3).
- `backend/tests/Directory.Build.props`: test projects are executables; `CA1707` allowed for `Method_Scenario_Expected` names.
- `backend/Dockerfile`: multi-stage image of the API (SDK and ASP.NET base images pinned by digest, locked restore, non-root `$APP_UID`, port 8080, `Production`). The LLM settings come from environment variables at run time. The CLI has no image.
- CI, mirroring ValidatorHub and ctf-janus (see the [README](../../README.md#ci)): `backend-ci.yml` and `security.yml` are implemented, with Dependabot on a 7-day cooldown. `frontend-ci.yml` and `e2e.yml` (Playwright) are planned.

## Decisions log

| Date | Topic | Decision | Reason |
| ---- | ----- | -------- | ------ |
| 2026-11-16 | Runtime | .NET 10 (LTS), ASP.NET Core and `Microsoft.Extensions.*` packages on the 10.0.x line. | The installed SDK is 10.0.301. ValidatorHub and ctf-janus use the same line. .NET 11 packages exist but are not LTS. |
| 2026-11-16 | Orchestration | Microsoft Agent Framework (`Microsoft.Agents.AI`, 1.x stable) on Microsoft.Extensions.AI, instead of LangChain or Semantic Kernel. | It is Microsoft's successor to Semantic Kernel and AutoGen. `IChatClient` gives streaming, multimodal content (`DataContent`) and usage (`UsageDetails`) out of the box. |
| 2026-11-16 | Direct calls (step 01) | OpenAI .NET SDK with its endpoint set to the LiteLLM proxy. | LiteLLM is a Python library; from .NET, its OpenAI-compatible proxy is the way to use it. |
| 2026-11-16 | API style | Minimal API with `TypedResults`, not controllers as in ValidatorHub and ctf-janus. | Small surface, and native SSE (`TypedResults.ServerSentEvents`) for streaming. |
| 2026-11-16 | Layering | ValidatorHub's `backend/src` + `backend/tests` layout without a `Domain` project. | There are no persisted entities. A `Domain` layer can be added if one appears. |
| 2026-11-16 | Secrets | `dotnet user-secrets`, one store shared by the API and the CLI, written by `dev.sh secrets` through stdin. | Same practice as the reference repos. One setup serves both executables, and the key never shows up in a process list. |
| 2026-11-16 | Tests | xUnit v3 on Microsoft.Testing.Platform, `WebApplicationFactory` in the `Testing` environment, and fake model clients. | Same as ValidatorHub. Tests and CI need no proxy and no secrets. |
| 2026-11-16 | Web app | Next.js + React + Fluent UI v9, port 3030 (planned). | Same stack as the ValidatorHub frontend and the ctf-janus admin. The port avoids clashing with them (3000, 3010). |
| 2026-11-16 | CI | `backend-ci.yml` = ValidatorHub's build job (MTP `--solution` tests) + ctf-janus's Docker job (Trivy binary verified by checksum, not `trivy-action`). `security.yml` copied unchanged (identical in both repos). | Same guarantees as the reference repos. ctf-janus moved away from `trivy-action` after it was compromised in 2026. |
| 2026-11-16 | Dependabot | Ignore semver-major updates of `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*` and the `dotnet/*` base images. | Keeps the backend on the .NET 10 LTS line. A move to 11.x is a manual change of the target framework, packages and images together. |
