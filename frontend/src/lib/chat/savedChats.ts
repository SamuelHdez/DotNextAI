import type { ChatMessage } from "@/lib/chat/conversation";
import type { ConversationDetail, ConversationSummary, Folder } from "@/services/types";

/** Rules of the saved chats, mirrored from the backend's `ConversationRules` (keep both in sync). */
export const MAX_FOLDER_NAME_LENGTH = 100;
export const MAX_TITLE_LENGTH = 200;
const AUTO_TITLE_LENGTH = 60;

/** The title the server gives a conversation before its first question; the UI shows its own translation. */
export const UNTITLED_TITLE = "New conversation";

/** Link of a saved chat: `/?c=<id>` opens it (docs/specs/feature-saved-chats.md#web-app). */
export const chatHref = (id: string) => `/?c=${encodeURIComponent(id)}`;

export type NameCheck = { ok: true; value: string } | { ok: false; reason: "empty" | "tooLong" | "control"; max: number };

/** Trimmed, 1..max characters, no control characters (folder names and titles). */
export function checkName(value: string, max: number): NameCheck {
  const trimmed = value.trim();
  if (trimmed.length === 0) return { ok: false, reason: "empty", max };
  if (trimmed.length > max) return { ok: false, reason: "tooLong", max };
  if (/[\u0000-\u001f\u007f-\u009f]/.test(trimmed)) return { ok: false, reason: "control", max };
  return { ok: true, value: trimmed };
}

/**
 * The title the server gives an untitled conversation from its first question (`ConversationRules.TitleFrom`): the
 * first line, spaces collapsed, cut at a word near 60 characters with "…". Used to show it before the server answers.
 */
export function titleFrom(question: string): string {
  const firstLine = question.trim().split("\n", 1)[0].trim().replace(/\s+/g, " ");
  if (firstLine.length <= AUTO_TITLE_LENGTH) return firstLine;
  const cut = firstLine.lastIndexOf(" ", AUTO_TITLE_LENGTH);
  return `${firstLine.slice(0, cut > AUTO_TITLE_LENGTH / 2 ? cut : AUTO_TITLE_LENGTH).trimEnd()}…`;
}

/** The conversation inserted or replaced, the most recently updated first (as GET /api/conversations orders them). */
export function upsertConversation(list: readonly ConversationSummary[], conversation: ConversationSummary): ConversationSummary[] {
  return [conversation, ...list.filter((existing) => existing.id !== conversation.id)].sort((a, b) =>
    b.updatedAt.localeCompare(a.updatedAt),
  );
}

/** Folders in name order (case-insensitive), as GET /api/folders orders them. */
export function sortFolders(folders: readonly Folder[]): Folder[] {
  return [...folders].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: "base" }));
}

/** The folders with their conversations and the conversations without a folder (also those of an unknown folder). */
export function groupByFolder(folders: readonly Folder[], conversations: readonly ConversationSummary[]) {
  const known = new Set(folders.map((folder) => folder.id));
  return {
    folders: folders.map((folder) => ({ folder, conversations: conversations.filter((c) => c.folderId === folder.id) })),
    unfiled: conversations.filter((c) => !c.folderId || !known.has(c.folderId)),
  };
}

/** The messages of a saved conversation as the chat shows them; image URLs are made absolute with `imageUrl`. */
export function toChatMessages(detail: ConversationDetail, imageUrl: (url: string) => string): ChatMessage[] {
  return detail.messages.map((message) => ({
    id: message.id,
    role: message.role,
    text: message.text,
    ...(message.images.length > 0 && {
      images: message.images.map((image, index) => ({ name: String(index + 1), src: imageUrl(image.url) })),
    }),
    ...(message.model && message.role === "assistant" && { model: message.model }),
    ...(message.truncated && { truncated: true }),
    ...(message.stopped && { stopped: true }),
    ...(message.usage && { usage: message.usage }),
  }));
}
