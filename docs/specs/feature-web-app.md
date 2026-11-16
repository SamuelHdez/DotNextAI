# Web app

> **Status:** Saved chats implemented on 2026-11-16, on top of steps 05-10 (web UI with chat, image input, streamed answers, model selector, images page, token metrics).

`frontend/` is the browser front end of DotNextAI: a Next.js app with Fluent UI v9 and the ERNI brand, laid out as a dashboard like ValidatorHub. It talks only to `DotNextAI.Api` ([API contract](../design/api.md)). The terminal app stays next to it ([CLI](feature-cli.md)).

## Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| Stack | Next.js 16 (App Router, standalone output), React 19, TypeScript 5, Fluent UI v9, Vitest + Testing Library. Same as the ValidatorHub frontend and the ctf-janus admin. No Tailwind: Fluent tokens and a minimal CSS reset are enough. | 2026-11-16 |
| Layout | ValidatorHub's dashboard shell: sticky header (hamburger, ERNI logo, product name, preferences) and a Fluent `NavDrawer`. Inline at 260 px with a collapsible 56 px icon rail, overlay below 640 px. | 2026-11-16 |
| Brand | ERNI Brand Book colours. Fluent brand ramp built on ERNI Dark Blue `#033778`, Source Sans 3 (next/font, self-hosted), full logo on light surfaces and white coat of arms on dark ones. Theme copied from ValidatorHub: system, light or dark, stored in `localStorage` (`dnai-theme`) and applied before first paint. | 2026-11-16 |
| Languages | English (default) and Spanish. A selector in the header switches at once, with no reload, so the chat survives. The choice is stored in the `dnai-locale` cookie (1 year), so the server renders the right language and `<html lang>` from the first byte. The browser language is not detected. | 2026-11-16 |
| i18n implementation | Own typed dictionaries (`src/lib/i18n/messages/en.ts`, `es.ts`) with `{name}` parameters and locale number formatting, no library. Two languages and about a hundred strings do not justify another dependency in the supply chain; a missing or extra Spanish key does not compile. | 2026-11-16 |
| Errors | The text comes from the API's `failure` code (`error.<Failure>` keys), never from its English `detail`. An unreachable API, a validation problem or an unknown status have their own texts. | 2026-11-16 |
| API access | The browser calls the API directly (`NEXT_PUBLIC_API_BASE_URL`, CORS on the API). The LiteLLM key stays in the API. | 2026-11-16 |
| Conversation | **Saved** since 2026-11-16 ([saved chats](feature-saved-chats.md)): the first question creates the chat, every question goes to `POST /api/conversations/{id}/messages/stream` and the server reads the history from the database. The open chat is in the address (`/chat?c=<id>`). This replaced "kept in the chat page's React state, lost on reload", which followed the old product rule "conversations live in the browser". | 2026-11-16 |
| Answers | Plain text with the model's line breaks (`white-space: pre-wrap`). Markdown rendering is a follow-up, because it needs a sanitizing renderer. | 2026-11-16 |
| Security | Strict CSP with a per-request nonce (`src/proxy.ts`). `connect-src` allows only the app and the API origin, so a model answer can never make the page call anywhere else. `img-src` adds the API origin for the saved chats' images. Static security headers come from ValidatorHub. No authentication: local use, like the API. | 2026-11-16 |
| Images (step 06) | Attach with a button or by pasting (e.g. a screenshot). The browser checks count, size and the file signature before reading or sending, as the backend does. Previews are `data:` URIs (the CSP's `img-src` already allows them), so there are no object URLs to revoke. | 2026-11-16 |
| Streaming (step 07) | The chat calls `POST /api/chat/stream` and reads its Server-Sent Events with a small reader of its own (`src/services/sse.ts`, about 30 lines, no library): `EventSource` cannot send a POST body. A **Stop** button replaces Send while an answer is written. | 2026-11-16 |
| Model selector (step 08) | A Fluent `Dropdown` labelled *Model* in the chat header, filled from `GET /api/models`. The choice is remembered in `localStorage` (`dnai-model`); a remembered model the server no longer offers falls back to the default, and choosing the default forgets the choice. Only a model other than the default is sent. The control keeps the same element in every state: wrapping it in a tooltip on failure re-mounted it and lost its state, so a described hint sits next to it instead. | 2026-11-16 |
| Dependencies | Versions at least 7 days old (`npm install --before`), npm only, `npm ci --ignore-scripts`. | 2026-11-16 |

## Pages

### Home (`/`)

A dashboard with four cards:

| Card | Content |
| ---- | ------- |
| Chat | Short description and an **Open chat** button. |
| Image generation | Short description and a **Create an image** button (step 09). |
| Service status | `GET /api/health`: API *Online* / *Unreachable*, language model *Configured* / *Not configured* / *Unknown*, with a hint (`./scripts/dev.sh`, `./scripts/dev.sh secrets`) and a refresh button. |
| Course backlog | The ten course steps in three phases, each *Done*, *In progress* or *Planned* (`src/lib/backlog.ts`, kept in sync with [product rules](product-rules.md#backlog)). |

### Images (`/images`)

Image generation (step 09): description, shape, Generate, and a gallery of the session's images with downloads. Details in [image generation](feature-images.md#web-app-images).

### Chat (`/chat`)

Next to the chat, the *Saved chats* panel: folders, chats, and their actions ([saved chats](feature-saved-chats.md#web-app)). `/chat?c=<id>` opens a saved chat.


- **Empty state:** "Ask anything" with three suggested questions; clicking one asks it.
- **Messages:** user messages on the right in ERNI blue, answers on the left with the model name. A cut answer shows *"The answer was cut off…"*. The list sits inside a `role="log"` live region (the region wraps the `<ol>`, so the list semantics stay) and scrolls to the newest message.
- **Composer:** a labelled textarea. Enter sends, Shift+Enter adds a line, and Enter while composing with an IME does nothing. The counter appears from 16,000 characters.
- **Images:**
  - Attaching: the paperclip button (*Attach images*) opens a file picker (PNG, JPEG, WebP, GIF, several at once), and pasting an image into the question attaches it.
  - Before sending: attachments show as 64 px thumbnails in a list (*Images for your next question*), each with a *Remove* button.
  - Rules: up to 4 images of 4 MB each, the format checked from the bytes. Errors under the field: *"notes.png is not a PNG, JPEG, WebP or GIF image."*, *"…is too large…"*, *"You can attach up to 4 images…"*. The button is disabled at 4.
  - Sending: images go in base64 with that question only and show above it in the conversation. On failure they come back with the question; *New conversation* discards them.
- **While waiting:** the question is shown with a *"DotNextAI is thinking…"* spinner until the first piece arrives. Then the answer grows in its bubble as it is written. Typing goes on, and **Send** becomes **Stop**.
- **Tokens:** each answer shows its tokens next to the model, and the conversation total sits above the composer with a hint that input tokens grow with the history ([token metrics](feature-token-metrics.md#formats)). A stopped answer has none.
- **Model:** the *Model* selector lists the proxy's chat models, the default as *"gpt-5.6-luna (default)"*. Changing it applies to the next questions and the conversation goes on. Each answer shows the model that wrote it. While the list loads the selector says *"Loading models…"*. If the list fails it is disabled at *"Default model"*, with the hint *"The model list could not be loaded; the default model answers."*
- **Screen readers:** the conversation region is `aria-busy` while an answer streams, so the answer is announced once, complete, instead of piece by piece.
- **Stop:** what had arrived stays in the conversation as the answer, with *"Stopped before the end."*, and is part of the history. With nothing yet, the question and its images go back to the composer.
- **Broken answer:** an `error` event or a stream that closes early drops the partial text and shows the failure like any other, with the question given back.
- **History:** the server keeps it: each question goes to the saved chat (`POST /api/conversations/{id}/messages/stream`) without `history`.
- **Failure:** an error bar with the localized text, which can be dismissed. The question goes back into the composer (unless something else was typed), and the conversation is unchanged.
- **Limits** (same as the backend): empty or longer than 20,000 characters is rejected before sending, with the message under the field. At 50 questions a warning asks for a new conversation and the composer is disabled.
- **New conversation** (header button) cancels the question in flight and empties the chat; the previous one stays in *Saved chats*.

## Running it

```bash
./scripts/dev.sh          # API :5100 + web :3030
./scripts/dev.sh web      # only the web app (the API must run for the chat)
```

`dev.sh` installs `node_modules` with `npm ci --ignore-scripts` when needed and writes `frontend/.env.local` with `NEXT_PUBLIC_API_BASE_URL=http://localhost:5100` (git-ignored, see `frontend/.env.example`).

## Verification

- `ChatWorkspace.test.tsx`: see [saved chats](feature-saved-chats.md#verification).
- `ChatView.test.tsx`:
  - Sending: suggestions; question and answer with the model; history sent with the next question; Enter and Shift+Enter; a suggestion click.
  - Problems: the failure shown in Spanish without the API's English detail, and the question given back; unreachable API; truncated answer.
  - Lifecycle: a new conversation without history; empty question.
- `ChatView.test.tsx` (streaming): the streaming endpoint is called; thinking, then partial text with `aria-busy` and Stop, then the complete answer; Stop keeps the partial answer (the request aborted); Stop before any text gives the question back; a broken stream shows the failure.
- `sse.test.ts`: events split across chunks (also inside a line and inside a UTF-8 character), CRLF, comments, multi-line data. `apiClient.test.ts` (`streamChat`): deltas and the whole answer, an `error` event, problem details before the stream, a stream ending without `done`.
- `ImagesView.test.tsx`: see [image generation](feature-images.md#verification).
- `ChatViewUsage.test.tsx`: see [token metrics](feature-token-metrics.md#verification).
- `ChatViewModels.test.tsx`: the default shown and not sent; the chosen model sent with the conversation going on and remembered; a remembered model restored; the default forgetting the choice; a list failure falling back (in Spanish). `initialModel` rules.
- `ChatViewLimit.test.tsx`: the question limit warning and the disabled composer.
- `ChatViewImages.test.tsx`:
  - Sending: preview and base64 payload, images not resent in the history.
  - Attachments: remove, non-image rejected (in Spanish), the limit of 4, paste.
  - Failure: images kept after a failed question.
- `images.test.ts`: signature detection, base64 and preview, the content winning over the declared type, size checked before reading.
- `ServiceStatusCard.test.tsx`: online and configured, not configured hint, unreachable hint (in Spanish).
- `LanguageMenu.test.tsx`: switching to Spanish updates the UI and the cookie; languages named in their own language. The button's accessible name starts with its visible code (`EN · Language: English`, WCAG 2.5.3).
- `SideNav.test.tsx`: links, current page, collapsed rail names.
- `I18nProvider.test.tsx`, `translate.test.ts`, `messages.test.ts`: the same keys and placeholders in both languages, number formatting, cookie and `<html lang>`.
- `chat.test.ts`, `apiClient.test.ts`: question rules, history, error mapping for every `failure`, the request shape, problem parsing, network and abort errors.
- `contentSecurityPolicy.test.ts`: nonce-only scripts and `connect-src` limited to the API.
- Theme and brand tests copied from ValidatorHub (`erniBrand`, `fluentThemeCss`, `themePreference`, `ErniLogo`, `streamedStyles`).
- Manual, 2026-11-16: `./scripts/dev.sh`, then home and chat in light and dark, server-rendered English and Spanish (`<title>`, `<html lang>`), the CSP header, and CORS from `http://localhost:3030`.
- Playwright E2E with axe (`frontend/e2e`, `e2e.yml`): home, chat (history across the whole chain, outage, thinking, new conversation), saved chats (reload, folders, stopped answers, images), language and theme kept after a reload. See [testing](../design/testing.md#end-to-end).
