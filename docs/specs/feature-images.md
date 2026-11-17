# Image generation

> **Status:** Step 09 implemented on 2026-11-16, in the web app, the CLI and the API. The real `gpt-image-2` is still to be tried: the proxy key's access is limited (see [product rules](product-rules.md#models-available-through-the-proxy)).

Users describe an image and the proxy's image model draws it. The service lives in Application (`DotNextAI.Application/Images`) and Infrastructure (`LiteLlmImageService`); the web app, the CLI and the API use it the same way.

## Decisions

| Topic | Decision | Date |
| ----- | -------- | ---- |
| Model | `Llm:ImageModel`, default `gpt-image-2` (`mode = image_generation` in the proxy). Chosen by the server, not by the client. | 2026-11-16 |
| Call | `POST {Llm:Endpoint}images/generations` with `{ model, prompt, n: 1, size }`, made directly with the named `HttpClient` (`LiteLlmHttp`, shared with the model catalog). Not the OpenAI SDK's `ImageClient`: gpt-image models always answer `b64_json` and reject `response_format`, and the direct call is fully under our control. | 2026-11-16 |
| No retries | A failed generation is not retried: a retry would draw (and bill) a second image. | 2026-11-16 |
| Sizes | `square` 1024×1024 (default), `landscape` 1536×1024, `portrait` 1024×1536 (the gpt-image sizes). | 2026-11-16 |
| Format | The answer's bytes decide the format (`ImageFormats.Detect`); anything that is not a PNG, JPEG, WebP or GIF is `EmptyAnswer`. | 2026-11-16 |
| Failures | The same `ChatFailure` values and HTTP mapping as the chat (`ModelUnavailable`, `ModelNotFound` for 403 model access, `BadRequest` for a refused prompt…), so every front end reuses its messages. | 2026-11-16 |
| Rate limit | Its own per-IP limit, `RateLimiting:ImagePermitsPerMinute`, default 5: an image costs far more than a question. | 2026-11-16 |
| Storage | None. The web app keeps the session's images in the page (lost when leaving it); the CLI writes a file. | 2026-11-16 |

## Rules

- The description is trimmed; 1-4,000 characters (`ImageRules.MaxPromptLength`). Invalid descriptions never reach the proxy.
- One image per call. The answer must contain `data[0].b64_json` with a supported image; otherwise *"The model returned no image."* (`EmptyAnswer`).
- `revised_prompt`, when the model reports it, is shown as *"The model drew: …"*.
- The generation's `usage` (step 10) is shown as tokens on the card and on the CLI's stderr.
- Logs hold the model, size in bytes and duration; never the description.

## Web app (`/images`)

- **Description:** textarea labelled *Description*. Empty or too long descriptions get an error under the field.
- **Shape:** radio group with *Square*, *Landscape* and *Portrait*.
- **Generate:** disabled while drawing, with *"Drawing the image… this can take up to a minute."*
- **Gallery:** the newest image first, each with its revised prompt (or the description), the model and a **Download** link (`dotnextai-image-<n>.png`). Previews are `data:` URIs.
- **Failures:** an error bar with the localized text of the `failure` code.
- **Navigation:** *Images* in the side navigation (the home dashboard and its *Image generation* card were removed on 2026-11-17).

## CLI (`dotnextai image`)

```bash
dotnextai image A fox reading by a lamp --size landscape --output fox.png
```

| Option | Meaning |
| ------ | ------- |
| `description...` | What to draw (required; quotes optional) |
| `--size`, `-s` | `square` (default), `landscape`, `portrait` |
| `--output`, `-o` | File to write; default `dotnextai-image-<yyyyMMdd-HHmmss>.png` in the current folder. The extension follows the real format. |
| `--force` | Overwrite an existing file. Without it an existing file is an error (exit 2) **before** generating, so no paid image is lost. |

stdout gets the full path of the saved file; the progress line and *"The model drew: …"* go to stderr. Exit codes: 0, 1 (generation or saving failed), 2 (usage, existing file), 3 (not configured), 130 (Ctrl+C).

## Verification

- `LiteLlmImageServiceTests`:
  - The request: route, key, body (`model`, trimmed `prompt`, `n`, `size`) and no `response_format`; the size mapping.
  - The answer: format from the bytes; answers without an image (`EmptyAnswer`); invalid JSON.
  - Errors: mapped like the chat's (no healthy deployments, a refused prompt, model access, key, rate limit); no retry.
  - Input: empty or too long prompts without HTTP; not configured.
- `ImageGenerationCommandTests` (CLI):
  - Output: the file saved and its path printed, the revised prompt; `--size`; the extension following the format.
  - Existing files: refused before generating; `--force`.
  - Errors: a failure with its hint; not configured; missing or too long description.
- `ImageEndpointTests`:
  - Answers: base64 answer; default size.
  - Errors: 400 for an empty prompt or an unknown size; 503 when not configured; failures with their status.
  - Its own rate limit, separate from the chat's.
- `ImagesView.test.tsx`: generate with preview and download name, shape sent, newest first, a Spanish failure, an empty description not sent, the drawing state.
- E2E `images.spec.ts`: an image through the real API and the fake proxy, shown and downloaded with axe; an outage; navigation.
