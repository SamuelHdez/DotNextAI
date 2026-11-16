import { ApiError, apiFetch, apiResponse } from "@/services/apiClient";
import { readServerSentEvents } from "@/services/sse";
import type { ChatRequest, ChatResponse, ProblemDetails } from "@/services/types";

/** `POST /api/chat`: the question after the previous turns of the conversation (the API keeps no state). */
export function sendChat(request: ChatRequest, signal?: AbortSignal): Promise<ChatResponse> {
  return apiFetch<ChatResponse>("/api/chat", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
    signal,
  });
}

/**
 * `POST /api/chat/stream` (step 07): calls `onDelta` with each piece of the answer as it arrives and resolves with the
 * whole answer. Failures before the first piece are problem details (an {@link ApiError}, as in {@link sendChat});
 * a failure after it arrives as an `error` event and rejects the same way. Aborting the signal stops the answer.
 */
export async function streamChat(request: ChatRequest, onDelta: (text: string) => void, signal?: AbortSignal): Promise<ChatResponse> {
  const response = await apiResponse("/api/chat/stream", {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
    body: JSON.stringify(request),
    signal,
  });
  if (!response.body) throw new ApiError("The answer stream is empty.", 0);

  for await (const { event, data } of readServerSentEvents(response.body)) {
    if (event === "delta") {
      onDelta((JSON.parse(data) as { text: string }).text);
    } else if (event === "done") {
      return JSON.parse(data) as ChatResponse;
    } else if (event === "error") {
      const problem = JSON.parse(data) as ProblemDetails;
      throw new ApiError(problem.detail ?? "The answer failed.", problem.status ?? 502, problem);
    }
  }

  // The connection closed before `done`: treated like an unreachable API.
  throw new ApiError("The answer stream ended before it was complete.", 0);
}
