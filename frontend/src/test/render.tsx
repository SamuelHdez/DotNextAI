import { FluentProvider, webLightTheme } from "@fluentui/react-components";
import { render, type RenderOptions } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import type { vi } from "vitest";
import { I18nProvider } from "@/lib/i18n/I18nProvider";
import type { Locale } from "@/lib/i18n/locales";

/** Renders inside Fluent UI and the i18n provider, as the app does. */
export function renderWithProviders(ui: ReactElement, { locale = "en", ...options }: { locale?: Locale } & RenderOptions = {}) {
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <FluentProvider theme={webLightTheme}>
        <I18nProvider initialLocale={locale}>{children}</I18nProvider>
      </FluentProvider>
    );
  }
  return render(ui, { wrapper: Wrapper, ...options });
}

/** A fetch Response with a JSON body. */
export function jsonResponse(body: unknown, status = 200, contentType = "application/json"): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": contentType } });
}

/** A text/event-stream Response with the given events (as POST /api/chat/stream sends them). */
export function sseResponse(...events: { event: string; data: unknown }[]): Response {
  const body = events.map(({ event, data }) => `event: ${event}\ndata: ${JSON.stringify(data)}\n\n`).join("");
  return new Response(body, { status: 200, headers: { "Content-Type": "text/event-stream" } });
}

/** A streamed answer: one `delta` per word, then `done`. */
export function streamedAnswer(text: string, truncated = false, model = "gpt-5.6-luna"): Response {
  const pieces = text.split(/(?<=\s)/).filter(Boolean);
  return sseResponse(...pieces.map((piece) => ({ event: "delta", data: { text: piece } })), { event: "done", data: { answer: text, model, truncated } });
}

/** A streamed Response whose events the test pushes one by one, to see the UI while the answer is being written. */
export function controlledStream() {
  let push!: (chunk: string) => void;
  let close!: () => void;
  const encoder = new TextEncoder();
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      push = (chunk) => controller.enqueue(encoder.encode(chunk));
      close = () => controller.close();
    },
  });
  return {
    response: new Response(body, { status: 200, headers: { "Content-Type": "text/event-stream" } }),
    delta: (text: string) => push(`event: delta\ndata: ${JSON.stringify({ text })}\n\n`),
    done: (answer: string) => {
      push(`event: done\ndata: ${JSON.stringify({ answer, model: "gpt-5.6-luna", truncated: false })}\n\n`);
      close();
    },
    error: (failure: string, status: number) => {
      push(`event: error\ndata: ${JSON.stringify({ status, failure, title: "x", detail: "English detail" })}\n\n`);
      close();
    },
  };
}

/** GET /api/models as the API answers it, with the two chat models of the ERNI proxy. */
export const MODELS = {
  defaultModel: "gpt-5.6-luna",
  models: [
    { id: "gpt-5.6-luna", isDefault: true, maxInputTokens: 922000, maxOutputTokens: 128000 },
    { id: "gpt-5.6-terra", isDefault: false, maxInputTokens: 922000, maxOutputTokens: 128000 },
  ],
};

/**
 * A fetch stub for the chat page: GET /api/models answers `models` (or fails with `models: "fail"`), every other call
 * goes to `chat`, so tests only see (and count) the chat calls.
 */
type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

export function chatPageFetch(chat: ReturnType<typeof vi.fn> | FetchLike, models: unknown = MODELS): FetchLike {
  return (input, init) => {
    if (String(input).endsWith("/api/models")) {
      return models === "fail" ? Promise.reject(new TypeError("Failed to fetch")) : Promise.resolve(jsonResponse(models));
    }
    return (chat as FetchLike)(input, init);
  };
}

