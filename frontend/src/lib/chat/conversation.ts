import type { ChatTurn } from "@/services/types";

/** A message shown in the chat. Assistant messages carry the model that answered and whether the answer was cut. */
export interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  text: string;
  model?: string;
  truncated?: boolean;
}

/** The history sent to the API: every message so far, oldest first. */
export function toHistory(messages: readonly ChatMessage[]): ChatTurn[] {
  return messages.map(({ role, text }) => ({ role, text }));
}

/** Questions answered in the conversation (each answered question is a user message followed by an answer). */
export function questionCount(messages: readonly ChatMessage[]): number {
  return messages.filter((message) => message.role === "user").length;
}
