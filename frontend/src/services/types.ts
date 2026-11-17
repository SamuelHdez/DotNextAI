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

/** Tokens of one call (step 10); input includes the history sent with the question. */
export interface TokenUsage {
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
}

export interface ChatResponse {
  answer: string;
  model: string;
  truncated: boolean;
  /** Null when the proxy does not report it. */
  usage?: TokenUsage | null;
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

/** Shape of a generated image (step 09): 1024×1024, 1536×1024 or 1024×1536. */
export type ImageSize = "square" | "landscape" | "portrait";

export interface ImageRequest {
  prompt: string;
  size: ImageSize;
}

/** A generated image: the file in base64 (no `data:` prefix) and its media type. */
export interface ImageResponse {
  mediaType: string;
  data: string;
  model: string;
  revisedPrompt: string | null;
  usage?: TokenUsage | null;
}


/* Saved chats (docs/design/api.md#saved-chats). */

/** `failure` values of the saved chats problems (StoreFailure on the backend). */
export const STORE_FAILURES = ["NotFound", "FolderNotFound", "DuplicateFolderName", "ConversationFull"] as const;

export type StoreFailure = (typeof STORE_FAILURES)[number];

/** Icon and colour keys of a folder (FolderAppearance on the backend). */
export const FOLDER_ICONS = ["folder", "briefcase", "star", "heart", "book", "code", "lightbulb", "rocket", "people", "home", "beaker", "airplane"] as const;
export type FolderIcon = (typeof FOLDER_ICONS)[number];
export const FOLDER_COLORS = ["default", "red", "orange", "yellow", "green", "teal", "blue", "purple", "berry", "pink", "brown"] as const;
export type FolderColor = (typeof FOLDER_COLORS)[number];

export interface Folder {
  id: string;
  name: string;
  icon: FolderIcon;
  color: FolderColor;
  conversationCount: number;
  createdAt: string;
}

export interface ConversationSummary {
  id: string;
  title: string;
  folderId: string | null;
  /** Model of the last answer; null before the first one. */
  model: string | null;
  questionCount: number;
  /** Sum of the answers' usage. */
  usage: TokenUsage;
  createdAt: string;
  updatedAt: string;
}

/** An image of a saved question; `url` (relative to the API) serves its bytes. */
export interface StoredImage {
  id: string;
  mediaType: string;
  size: number;
  url: string;
}

export interface StoredMessage {
  id: string;
  role: ChatRole;
  text: string;
  images: StoredImage[];
  model: string | null;
  usage: TokenUsage | null;
  truncated: boolean;
  stopped: boolean;
  createdAt: string;
}

export interface ConversationDetail {
  conversation: ConversationSummary;
  messages: StoredMessage[];
}

/** Body of PATCH /api/folders/{id}: what to change (at least one). */
export interface FolderChanges {
  name?: string;
  icon?: FolderIcon;
  color?: FolderColor;
}

/** Body of POST /api/conversations/{id}/messages/stream: a chat request without history (the server has it). */
export interface ConversationMessageRequest {
  question: string;
  images?: ChatImage[];
  model?: string;
}

/** The `done` event of a saved conversation: the answer and the conversation as saved. */
export interface ConversationChatResponse extends ChatResponse {
  conversation?: ConversationSummary | null;
}
