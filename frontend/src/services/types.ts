/** Contracts of DotNextAI.Api, mirrored from docs/design/api.md (keep both in sync). */

export interface HealthStatus {
  status: string;
  service: string;
  environment: string;
  llmConfigured: boolean;
  timestampUtc: string;
}

export type ChatRole = "user" | "assistant";

export interface ChatTurn {
  role: ChatRole;
  text: string;
}

export interface ChatRequest {
  question: string;
  history: ChatTurn[];
}

export interface ChatResponse {
  answer: string;
  model: string;
  truncated: boolean;
}

/** `failure` values of the API's problems (ChatFailure on the backend). */
export const CHAT_FAILURES = [
  "NotConfigured",
  "Unauthorized",
  "ModelNotFound",
  "ModelUnavailable",
  "BadRequest",
  "RateLimited",
  "Timeout",
  "Unavailable",
  "EmptyAnswer",
] as const;

export type ChatFailure = (typeof CHAT_FAILURES)[number];

/** RFC 9457 problem details as returned by the API, with DotNextAI's `failure` extension. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
  failure?: string;
}
