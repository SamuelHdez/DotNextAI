import { apiFetch } from "@/services/apiClient";
import type { HealthStatus } from "@/services/types";

export function getHealth(signal?: AbortSignal): Promise<HealthStatus> {
  return apiFetch<HealthStatus>("/api/health", { signal, cache: "no-store" });
}
