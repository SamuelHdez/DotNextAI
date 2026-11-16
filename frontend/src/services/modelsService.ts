import { apiFetch } from "@/services/apiClient";
import type { ModelsResponse } from "@/services/types";

/** `GET /api/models` (step 08): the chat models of the proxy, the default first. */
export function getModels(signal?: AbortSignal): Promise<ModelsResponse> {
  return apiFetch<ModelsResponse>("/api/models", { signal });
}
