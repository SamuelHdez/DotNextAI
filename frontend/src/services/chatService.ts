import { apiFetch } from "@/services/apiClient";
import type { ChatRequest, ChatResponse } from "@/services/types";

/** `POST /api/chat`: the question after the previous turns of the conversation (the API keeps no state). */
export function sendChat(request: ChatRequest, signal?: AbortSignal): Promise<ChatResponse> {
  return apiFetch<ChatResponse>("/api/chat", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
    signal,
  });
}
