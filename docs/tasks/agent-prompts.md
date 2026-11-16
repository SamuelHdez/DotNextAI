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
