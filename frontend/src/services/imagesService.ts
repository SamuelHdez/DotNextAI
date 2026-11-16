import { apiFetch } from "@/services/apiClient";
import type { ImageRequest, ImageResponse } from "@/services/types";

/** `POST /api/images` (step 09): one image from a description, drawn by the proxy's image model. */
export function generateImage(request: ImageRequest, signal?: AbortSignal): Promise<ImageResponse> {
  return apiFetch<ImageResponse>("/api/images", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
    signal,
  });
}
