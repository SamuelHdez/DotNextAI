import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, apiFetch } from "@/services/apiClient";
import { sendChat, streamChat } from "@/services/chatService";
import { jsonResponse, sseResponse, streamedAnswer } from "@/test/render";

afterEach(() => vi.unstubAllGlobals());

describe("apiFetch", () => {
  it("calls the API base URL and returns the JSON", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ status: "Healthy" }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(apiFetch("/api/health")).resolves.toEqual({ status: "Healthy" });
    expect(String(fetchMock.mock.calls[0][0])).toBe("http://api.test/api/health");
  });

  it("turns an error answer into an ApiError with the problem details", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(jsonResponse({ status: 503, failure: "ModelUnavailable", detail: "down" }, 503, "application/problem+json")),
    );

    const error = await apiFetch("/api/chat").catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(503);
    expect((error as ApiError).problem?.failure).toBe("ModelUnavailable");
  });

  it("keeps a non-JSON error answer without problem details", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("Bad gateway", { status: 502, headers: { "Content-Type": "text/html" } })));

    const error = (await apiFetch("/api/chat").catch((e: unknown) => e)) as ApiError;

    expect(error.status).toBe(502);
    expect(error.problem).toBeUndefined();
  });

  it("reports an unreachable API as a network error", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    const error = (await apiFetch("/api/health").catch((e: unknown) => e)) as ApiError;

    expect(error.isNetworkError).toBe(true);
  });

  it("lets an aborted call reject with the abort error", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new DOMException("Aborted", "AbortError")));

    await expect(apiFetch("/api/health")).rejects.toMatchObject({ name: "AbortError" });
  });
});

describe("sendChat", () => {
  it("posts the question and the history as JSON", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ answer: "Ana", model: "gpt-5.6-luna", truncated: false }));
    vi.stubGlobal("fetch", fetchMock);

    const answer = await sendChat({ question: "What is my name?", history: [{ role: "user", text: "My name is Ana." }] });

    expect(answer.answer).toBe("Ana");
    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(String(url)).toBe("http://api.test/api/chat");
    expect(init.method).toBe("POST");
    expect(new Headers(init.headers).get("Content-Type")).toBe("application/json");
    expect(JSON.parse(init.body as string)).toEqual({ question: "What is my name?", history: [{ role: "user", text: "My name is Ana." }] });
  });
});

describe("streamChat", () => {
  it("passes every piece to onDelta and resolves with the whole answer", async () => {
    const fetchMock = vi.fn().mockResolvedValue(streamedAnswer("Hello Ana!"));
    vi.stubGlobal("fetch", fetchMock);
    const pieces: string[] = [];

    const answer = await streamChat({ question: "Hi", history: [] }, (piece) => pieces.push(piece));

    expect(pieces).toEqual(["Hello ", "Ana!"]);
    expect(answer).toEqual({ answer: "Hello Ana!", model: "gpt-5.6-luna", truncated: false });
    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(String(url)).toBe("http://api.test/api/chat/stream");
    expect(new Headers(init.headers).get("Accept")).toBe("text/event-stream");
  });

  it("rejects with the failure of an error event", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        sseResponse(
          { event: "delta", data: { text: "Half" } },
          { event: "error", data: { status: 502, failure: "Unavailable", title: "x", detail: "Dropped." } },
        ),
      ),
    );

    const error = (await streamChat({ question: "Hi", history: [] }, () => {}).catch((e: unknown) => e)) as ApiError;

    expect(error.status).toBe(502);
    expect(error.problem?.failure).toBe("Unavailable");
  });

  it("rejects with the problem details of a failure before the stream", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse({ status: 503, failure: "ModelUnavailable" }, 503, "application/problem+json")));

    const error = (await streamChat({ question: "Hi", history: [] }, () => {}).catch((e: unknown) => e)) as ApiError;

    expect(error.problem?.failure).toBe("ModelUnavailable");
  });

  it("treats a stream that ends without done as a network failure", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(sseResponse({ event: "delta", data: { text: "Half" } })));

    const error = (await streamChat({ question: "Hi", history: [] }, () => {}).catch((e: unknown) => e)) as ApiError;

    expect(error.isNetworkError).toBe(true);
  });
});

