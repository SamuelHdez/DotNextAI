import type { ChatTurn, TokenUsage } from "@/services/types";

/** A message shown in the chat. Assistant messages carry the model that answered and whether the answer was cut. */
export interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  text: string;
  /** Images sent with a user message, for display (they are not resent with later questions). */
  images?: { name: string; src: string }[];
  model?: string;
  truncated?: boolean;
  /** The user stopped the answer before the end (step 07); `text` is what had arrived. */
  stopped?: boolean;
  /** Tokens of the call that wrote this answer (step 10), when the proxy reported them. */
  usage?: TokenUsage;
}

/** The history sent to the API: every message so far, oldest first. */
export function toHistory(messages: readonly ChatMessage[]): ChatTurn[] {
  return messages.map(({ role, text }) => ({ role, text }));
}

/** Questions answered in the conversation (each answered question is a user message followed by an answer). */
export function questionCount(messages: readonly ChatMessage[]): number {
  return messages.filter((message) => message.role === "user").length;
}

/**
 * What the conversation cost so far (step 10): the sum of every answer's tokens, history resends included. `answers`
 * counts the answers whose usage is known; null when none is.
 */
export function conversationUsage(messages: readonly ChatMessage[]): (TokenUsage & { answers: number }) | null {
  const counted = messages.filter((message) => message.usage);
  if (counted.length === 0) return null;
  return counted.reduce(
    (total, { usage }) => ({
      inputTokens: total.inputTokens + usage!.inputTokens,
      outputTokens: total.outputTokens + usage!.outputTokens,
      totalTokens: total.totalTokens + usage!.totalTokens,
      answers: total.answers + 1,
    }),
    { inputTokens: 0, outputTokens: 0, totalTokens: 0, answers: 0 },
  );
}

