# Testing

> **Status:** All layers implemented on 2026-11-16, Playwright E2E included. Run everything with `./scripts/dev.sh test` and `./scripts/dev.sh e2e`.

No test calls the real LiteLLM proxy or needs a secret. CI runs every layer on each change to the code it covers.

## Layers

| Layer | Where | Tools | What it covers | CI |
| ----- | ----- | ----- | -------------- | -- |
| .NET unit | `backend/tests/DotNextAI.UnitTests` | xUnit v3, Microsoft.Testing.Platform | Chat rules, options, the agent service over a stub `HttpMessageHandler` (request shape, history, error mapping), `ChatConversation`, the CLI commands with a fake `IChatService` | `backend-ci.yml` |
| .NET integration | `backend/tests/DotNextAI.IntegrationTests` | `WebApplicationFactory` (`Testing` environment) | HTTP endpoints: health, chat validation, status codes and `failure`, rate limit, CORS | `backend-ci.yml` |
| Web unit and component | `frontend/src/**/*.test.ts(x)` | Vitest, Testing Library, jsdom, stubbed `fetch` | i18n catalogs, API client, chat rules and errors, chat page, status card, language menu, navigation, CSP, theme | `frontend-ci.yml` |
| End to end | `frontend/e2e/*.spec.ts` | Playwright (Chromium), axe-core | The whole chain in a real browser, plus accessibility | `e2e.yml` |

## End to end

### Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| What runs | The **production build** of the web app (`next build` + `next start`), the **real API** (`dotnet run`, Release) and a **fake LiteLLM proxy** (`frontend/e2e/fake-litellm.mjs`). The chain browser → Next.js → API → Agent Framework → OpenAI SDK → "proxy" is the one users get. | 2026-11-16 |
| Why a fake proxy, not a fake service | Faking `IChatService` would need test code in the API binary and would skip the agent, the SDK and the error mapping. The fake proxy speaks the OpenAI protocol, so the API is configured exactly as for the real one (`Llm:Endpoint` = `http://127.0.0.1:4010/`, allowed because it is loopback). | 2026-11-16 |
| Secrets | None. The API runs in the `E2E` environment (not Development, so the developer's user-secrets are never loaded) with a key generated per run; the fake proxy only checks that a bearer key is sent. | 2026-11-16 |
| Ports | Fake proxy 4010, API 5199, web 3039, so the suite runs next to `./scripts/dev.sh` (5100/3030). Playwright starts all three (`webServer`) and reuses running ones locally, never in CI. | 2026-11-16 |
| Accessibility | Every page under test passes axe with WCAG 2.2 A/AA tags and **no violation of any impact**. Tabster's focus sentinels (`[data-tabster-dummy]`) are excluded, as in the ctf-janus admin suite. | 2026-11-16 |
| Browser | Chromium only, one worker, `reducedMotion: "reduce"`; one retry in CI with traces and screenshots kept on failure. | 2026-11-16 |

### Fake LiteLLM

| Last user message | Answer |
| ----------------- | ------ |
| contains `[e2e:unavailable]` | LiteLLM's real 400 "no healthy deployments" body, so the UI shows `ModelUnavailable` |
| contains `[e2e:slow]` | waits 1.5 s, then streams a word every 400 ms, so the thinking state, the partial answer and Stop can be seen |
| contains `[e2e:drop]` | streaming only: sends the first word, then drops the connection |
| image parts | `I see N image(s) (types): <question>`, with `invalid` for any image that is not a base64 data URI. Proves the image crosses the whole chain in the right format |
| "what is my name" | `Your name is X.` from an earlier "My name is X" in the history, or `I do not know your name yet.` Proves that the history crosses the whole chain |
| anything else | `Echo: <question>` |

With `"stream": true` it answers in OpenAI's `chat.completion.chunk` Server-Sent Events format, one word per chunk, ending with `data: [DONE]`. It answers 401 without a bearer key and 404 on unknown routes. Model name: `e2e-model`.

### Scenarios

- `home.spec.ts`:
  - Dashboard: title, the real API online with the model configured, the backlog.
  - Navigation: from the card and the side navigation, collapse to the icon rail.
  - axe.
- `chat.spec.ts`:
  - Answers: answer with model, history ("Ana"), suggestion click, Shift+Enter.
  - Streaming: thinking, then a partial answer, then the complete one; Stop keeps what arrived; a connection dropped mid-answer shows the failure and gives the question back.
  - Failures and reset: outage message with the question given back, new conversation forgets the history.
  - Images: an image reaches the model as a base64 PNG data URI and shows in the conversation; a non-image is rejected before sending; axe with attachments.
  - axe, empty and with messages.
- `preferences.spec.ts`:
  - Language: Spanish switch kept after a reload and rendered by the server (`<title>`, `<html lang>`); the chat survives a language change.
  - Theme: dark theme kept after a reload; axe in dark mode.

### Running

```bash
./scripts/dev.sh e2e                 # builds the API, installs Chromium, runs the suite
npm --prefix frontend run e2e        # same, once the API is built (dotnet build backend/DotNextAI.slnx -c Release)
npx --prefix frontend playwright test e2e/chat.spec.ts --headed   # one file, with a visible browser
```

On failure, `frontend/test-results/` keeps a screenshot and a trace per test (`npx playwright show-trace …`); CI uploads them with the HTML report as the `e2e-results` artifact.

### Findings fixed when the suite was added (2026-11-16)

- `label-content-name-mismatch` on the language button. Its visible "EN" was not part of its name "Language: English", so voice control users could not say what they see. The name is now `EN · Language: English`; axe matches whole words, so the code is not wrapped in parentheses.
- `listitem` in the chat. `role="log"` sat on the `<ol>` itself, which dropped its list semantics; the live region now wraps the list.
