import { FluentProvider, webLightTheme } from "@fluentui/react-components";
import { render, type RenderOptions } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import type { vi } from "vitest";
import { I18nProvider } from "@/lib/i18n/I18nProvider";
import type { Locale } from "@/lib/i18n/locales";
import type { ConversationDetail, ConversationSummary, Folder } from "@/services/types";

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

/** A streamed answer: one `delta` per word, then `done` (with `usage` when given). */
export function streamedAnswer(
  text: string,
  truncated = false,
  model = "gpt-5.6-luna",
  usage: { inputTokens: number; outputTokens: number; totalTokens: number } | null = null,
): Response {
  const pieces = text.split(/(?<=\s)/).filter(Boolean);
  return sseResponse(
    ...pieces.map((piece) => ({ event: "delta", data: { text: piece } })),
    { event: "done", data: { answer: text, model, truncated, usage } },
  );
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

type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

const NO_USAGE = { inputTokens: 0, outputTokens: 0, totalTokens: 0 };

/** A saved conversation as the API lists it. */
export function conversationSummary(id: string, overrides: Partial<ConversationSummary> = {}): ConversationSummary {
  return {
    id,
    title: "New conversation",
    folderId: null,
    model: null,
    questionCount: 0,
    usage: NO_USAGE,
    createdAt: "2026-11-16T10:00:00Z",
    updatedAt: "2026-11-16T10:00:00Z",
    ...overrides,
  };
}

/**
 * The saved chats endpoints in memory (lists, create, read one, rename, move, delete). Asking
 * (`…/messages/stream`) is not here: it goes to the test's chat mock.
 */
export function savedChatsBackend({
  folders = [] as Folder[],
  conversations = [] as ConversationSummary[],
  details = {} as Record<string, ConversationDetail>,
} = {}) {
  let created = 0;
  const state = { folders: [...folders], conversations: [...conversations], details: { ...details } };
  const notFound = () => jsonResponse({ status: 404, title: "Not found", failure: "NotFound" }, 404, "application/problem+json");

  function handle(url: URL, init?: RequestInit): Response | undefined {
    const method = init?.method ?? "GET";
    const body = init?.body ? (JSON.parse(init.body as string) as Record<string, unknown>) : {};
    const path = url.pathname;
    if (path === "/api/folders" && method === "GET") return jsonResponse(state.folders);
    if (path === "/api/folders" && method === "POST") {
      if (state.folders.some((f) => f.name.toLowerCase() === String(body.name).toLowerCase())) {
        return jsonResponse({ status: 409, failure: "DuplicateFolderName" }, 409, "application/problem+json");
      }
      const folder: Folder = { id: `f${++created}`, name: String(body.name), conversationCount: 0, createdAt: "2026-11-16T10:00:00Z" };
      state.folders.push(folder);
      return jsonResponse(folder, 201);
    }
    const folderMatch = /^\/api\/folders\/([^/]+)$/.exec(path);
    if (folderMatch) {
      const folder = state.folders.find((f) => f.id === folderMatch[1]);
      if (!folder) return notFound();
      if (method === "DELETE") {
        state.folders = state.folders.filter((f) => f !== folder);
        return new Response(null, { status: 204 });
      }
      folder.name = String(body.name);
      return jsonResponse(folder);
    }
    if (path === "/api/conversations" && method === "GET") return jsonResponse(state.conversations);
    if (path === "/api/conversations" && method === "POST") {
      const conversation = conversationSummary(`c${++created}`, { folderId: (body.folderId as string | undefined) ?? null });
      state.conversations.unshift(conversation);
      return jsonResponse(conversation, 201);
    }
    const match = /^\/api\/conversations\/([^/]+)(\/folder)?$/.exec(path);
    if (match) {
      const conversation = state.conversations.find((c) => c.id === match[1]);
      if (!conversation) return notFound();
      if (method === "GET") return jsonResponse(state.details[conversation.id] ?? { conversation, messages: [] });
      if (method === "DELETE") {
        state.conversations = state.conversations.filter((c) => c !== conversation);
        return new Response(null, { status: 204 });
      }
      if (match[2]) conversation.folderId = (body.folderId as string | null) ?? null;
      else conversation.title = String(body.title);
      return jsonResponse({ ...conversation });
    }
    return undefined;
  }

  return { state, handle };
}

/**
 * A fetch stub for the chat page: GET /api/models answers `models` (or fails with `models: "fail"`), the saved chats
 * endpoints are served by `saved` (empty by default), and every other call (the answers) goes to `chat`, so tests only
 * see (and count) the questions.
 */
export function chatPageFetch(
  chat: ReturnType<typeof vi.fn> | FetchLike,
  models: unknown = MODELS,
  saved: ReturnType<typeof savedChatsBackend> = savedChatsBackend(),
): FetchLike {
  return (input, init) => {
    const url = new URL(String(input));
    if (url.pathname === "/api/models") {
      return models === "fail" ? Promise.reject(new TypeError("Failed to fetch")) : Promise.resolve(jsonResponse(models));
    }
    const answer = saved.handle(url, init);
    return answer ? Promise.resolve(answer) : (chat as FetchLike)(input, init);
  };
}

