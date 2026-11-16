# Agent prompts

History of the prompts that shaped DotNextAI, with the outcome of each. Newest last. Paraphrased in English; the originals were in Spanish.

## 2026-11-16 — Course analysis

> Analyze the course exercise *Project 1: Basic LLM Chatbot App* (ten steps in three phases) for the DotNextAI repository. Analysis only.

Outcome: the repository was empty. A step-by-step mapping was proposed, and the open choices (LLM provider, web stack, runtime) were raised.

## 2026-11-16 — Stack change

> The course now allows any backend. Use .NET with Semantic Kernel instead?

Outcome: .NET was confirmed as a good fit. Microsoft Agent Framework was recommended over Semantic Kernel as its successor.

## 2026-11-16 — Stack and repository shape

> Use Microsoft Agent Framework. The frontend will be Next.js + React + Fluent UI. Add GitHub Actions CI/CD. The repository layout and pipelines must match ValidatorHub and ctf-janus.

Outcome: both reference repos were analyzed, and a layout plus the list of pipelines to mirror were proposed. Decisions were recorded in [architecture](../design/architecture.md#decisions-log).

## 2026-11-16 — Step 00: backend skeleton and docs

> ERNI LiteLLM proxy with URL and token. Playwright E2E: yes. Code, docs and commits in English, multi-language UI. Working from Windows. Add a `dev.sh`. Go step by step: first only the backend project setup, `AGENTS.md` and the documentation.

Outcome: step 00 in [todo.md](todo.md). The backend solution builds with 0 warnings, all tests pass, and `dev.sh` and the docs are written. CI, frontend and Docker are left for later steps.

## 2026-11-16 — Initial commit and backend CI

> Commit, then set up the pipelines.

Outcome: initial commit `be84b7c` (the user pushes it). Added `backend-ci.yml`, `security.yml`, `dependabot.yml` and `backend/Dockerfile`, mirroring ValidatorHub and ctf-janus. Checked locally with zizmor 1.30.1, Semgrep 1.180.0 (CI rulesets) and Gitleaks 8.30.1: no findings. The Docker job was not run locally.

## 2026-11-16 — Step 01: terminal Q&A

> CI is green. Go with step 01, the terminal. Document all the specs. Even with a web app later, the terminal project must persist.

Outcome: `dotnextai ask` through the LiteLLM proxy with the OpenAI .NET SDK, behind an `IChatService` contract the API will reuse. New specs: [CLI](../specs/feature-cli.md) (permanent deliverable, with a roadmap for later steps) and [chat service](../specs/feature-llm-chat.md). The first real call showed that every chat model of the proxy had *"no healthy deployments"*, so that error got its own failure type and the proxy's error detail is now shown (key masked). 68 tests green; a real answer is still to be confirmed.
