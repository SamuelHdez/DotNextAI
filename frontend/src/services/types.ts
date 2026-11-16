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

/** An image attached to the question: its media type and the file bytes in base64 (no `data:` prefix). */
export interface ChatImage {
  mediaType: string;
  data: string;
}

export interface ChatRequest {
  question: string;
  history: ChatTurn[];
  /** Images for this question only (step 06); the history stays text. */
  images?: ChatImage[];
  /** Chat model from GET /api/models (step 08); omitted for the default. */
  model?: string;
}

/** A chat model of the proxy; token limits are null when the proxy does not report them. */
export interface ChatModel {
  id: string;
  isDefault: boolean;
  maxInputTokens: number | null;
  maxOutputTokens: number | null;
}

export interface ModelsResponse {
  defaultModel: string;
  models: ChatModel[];
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
