# Web app

> **Status:** Step 06 (image input) implemented on 2026-11-16, on top of step 05 (web UI with chat).

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
| Conversation | Kept in the chat page's React state: one tab, lost on reload or when leaving the page (the question in flight is cancelled). It follows the product rule "conversations live in the browser". | 2026-11-16 |
| Answers | Plain text with the model's line breaks (`white-space: pre-wrap`). Markdown rendering is a follow-up, because it needs a sanitizing renderer. | 2026-11-16 |
| Security | Strict CSP with a per-request nonce (`src/proxy.ts`). `connect-src` allows only the app and the API origin, so a model answer can never make the page call anywhere else. Static security headers come from ValidatorHub. No authentication: local use, like the API. | 2026-11-16 |
| Images (step 06) | Attach with a button or by pasting (e.g. a screenshot). The browser checks count, size and the file signature before reading or sending, as the backend does. Previews are `data:` URIs (the CSP's `img-src` already allows them), so there are no object URLs to revoke. | 2026-11-16 |
| Dependencies | Versions at least 7 days old (`npm install --before`), npm only, `npm ci --ignore-scripts`. | 2026-11-16 |

## Pages

### Home (`/`)

A dashboard with three cards:

| Card | Content |
| ---- | ------- |
| Chat | Short description and an **Open chat** button. |
| Service status | `GET /api/health`: API *Online* / *Unreachable*, language model *Configured* / *Not configured* / *Unknown*, with a hint (`./scripts/dev.sh`, `./scripts/dev.sh secrets`) and a refresh button. |
| Course backlog | The ten course steps in three phases, each *Done*, *In progress* or *Planned* (`src/lib/backlog.ts`, kept in sync with [product rules](product-rules.md#backlog)). |

### Chat (`/chat`)

- **Empty state:** "Ask anything" with three suggested questions; clicking one asks it.
- **Messages:** user messages on the right in ERNI blue, answers on the left with the model name. A cut answer shows *"The answer was cut off…"*. The list sits inside a `role="log"` live region (the region wraps the `<ol>`, so the list semantics stay) and scrolls to the newest message.
- **Composer:** a labelled textarea. Enter sends, Shift+Enter adds a line, and Enter while composing with an IME does nothing. The counter appears from 16,000 characters.
- **Images:**
  - Attaching: the paperclip button (*Attach images*) opens a file picker (PNG, JPEG, WebP, GIF, several at once), and pasting an image into the question attaches it.
  - Before sending: attachments show as 64 px thumbnails in a list (*Images for your next question*), each with a *Remove* button.
  - Rules: up to 4 images of 4 MB each, the format checked from the bytes. Errors under the field: *"notes.png is not a PNG, JPEG, WebP or GIF image."*, *"…is too large…"*, *"You can attach up to 4 images…"*. The button is disabled at 4.
  - Sending: images go in base64 with that question only and show above it in the conversation. On failure they come back with the question; *New conversation* discards them.
- **While waiting:** the question is shown with a *"DotNextAI is thinking…"* spinner. Typing goes on, sending is disabled.
- **History:** every question is sent with the whole conversation (`POST /api/chat`, `history`).
- **Failure:** an error bar with the localized text, which can be dismissed. The question goes back into the composer (unless something else was typed), and the conversation is unchanged.
- **Limits** (same as the backend): empty or longer than 20,000 characters is rejected before sending, with the message under the field. At 50 questions a warning asks for a new conversation and the composer is disabled.
- **New conversation** (header button) cancels the question in flight and empties the chat.

## Running it

```bash
./scripts/dev.sh          # API :5100 + web :3030
./scripts/dev.sh web      # only the web app (the API must run for the chat)
```

`dev.sh` installs `node_modules` with `npm ci --ignore-scripts` when needed and writes `frontend/.env.local` with `NEXT_PUBLIC_API_BASE_URL=http://localhost:5100` (git-ignored, see `frontend/.env.example`).

## Verification

- `ChatView.test.tsx`:
  - Sending: suggestions; question and answer with the model; history sent with the next question; Enter and Shift+Enter; a suggestion click.
  - Problems: the failure shown in Spanish without the API's English detail, and the question given back; unreachable API; truncated answer.
  - Lifecycle: a new conversation without history; empty question.
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
- Playwright E2E with axe (`frontend/e2e`, `e2e.yml`): home, chat (history across the whole chain, outage, thinking, new conversation), language and theme kept after a reload. See [testing](../design/testing.md#end-to-end).
