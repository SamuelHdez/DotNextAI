import { appConfig } from "@/lib/config";
import type { ProblemDetails } from "@/services/types";

/** A failed API call. `status` 0 means the API could not be reached at all. */
export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly problem?: ProblemDetails,
  ) {
    super(message);
    this.name = "ApiError";
  }

  get isNetworkError(): boolean {
    return this.status === 0;
  }
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  const type = response.headers.get("content-type") ?? "";
  if (!type.includes("json")) return undefined;
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return undefined;
  }
}

/** Calls the API from the browser and returns its JSON; errors become {@link ApiError} with the problem details. */
export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  if (!appConfig.apiBaseUrl) {
    throw new ApiError("NEXT_PUBLIC_API_BASE_URL is not configured.", 0);
  }

  const url = new URL(path, appConfig.apiBaseUrl);
  let response: Response;
  try {
    response = await fetch(url, { ...init, headers: { Accept: "application/json", ...init?.headers } });
  } catch (error) {
    // Aborted calls are the caller's decision, not an API failure.
    if (error instanceof DOMException && error.name === "AbortError") throw error;
    throw new ApiError(`Could not reach ${url.origin}.`, 0);
  }

  if (!response.ok) {
    throw new ApiError(`Request to ${url.pathname} failed with status ${response.status}.`, response.status, await readProblem(response));
  }

  return (await response.json()) as T;
}
