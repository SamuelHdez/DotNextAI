import { apiFetch, apiResponse, apiSend } from "@/services/apiClient";
import { readChatStream } from "@/services/chatService";
import type {
  ConversationChatResponse,
  ConversationDetail,
  ConversationMessageRequest,
  ConversationSummary,
  Folder,
  FolderChanges,
} from "@/services/types";

/** Saved chats and folders (docs/design/api.md#saved-chats). */

const json = (method: string, body: unknown, signal?: AbortSignal): RequestInit => ({
  method,
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
  signal,
});

const path = (id: string) => `/api/conversations/${encodeURIComponent(id)}`;
const folderPath = (id: string) => `/api/folders/${encodeURIComponent(id)}`;

export const listFolders = (signal?: AbortSignal) => apiFetch<Folder[]>("/api/folders", { signal });
export const createFolder = (name: string) => apiFetch<Folder>("/api/folders", json("POST", { name }));
export const updateFolder = (id: string, changes: FolderChanges) => apiFetch<Folder>(folderPath(id), json("PATCH", changes));
export const deleteFolder = (id: string) => apiSend(folderPath(id), { method: "DELETE" });

export const listConversations = (signal?: AbortSignal) => apiFetch<ConversationSummary[]>("/api/conversations", { signal });
export const createConversation = (folderId: string | null = null, signal?: AbortSignal) =>
  apiFetch<ConversationSummary>("/api/conversations", json("POST", folderId ? { folderId } : {}, signal));
export const getConversation = (id: string, signal?: AbortSignal) => apiFetch<ConversationDetail>(path(id), { signal });
export const renameConversation = (id: string, title: string) => apiFetch<ConversationSummary>(path(id), json("PATCH", { title }));
export const moveConversation = (id: string, folderId: string | null) =>
  apiFetch<ConversationSummary>(`${path(id)}/folder`, json("PUT", { folderId }));
export const deleteConversation = (id: string) => apiSend(path(id), { method: "DELETE" });

/**
 * `POST /api/conversations/{id}/messages/stream`: like `streamChat`, but the server reads the history from the database
 * and saves the exchange. The `done` data carries the conversation as saved.
 */
export async function streamConversation(
  id: string,
  request: ConversationMessageRequest,
  onDelta: (text: string) => void,
  signal?: AbortSignal,
): Promise<ConversationChatResponse> {
  const response = await apiResponse(`${path(id)}/messages/stream`, {
    ...json("POST", request, signal),
    headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
  });
  return readChatStream<ConversationChatResponse>(response, onDelta);
}
