import type { MessageKey } from "@/lib/i18n/messages/en";
import type { MessageParams } from "@/lib/i18n/translate";
import { ApiError } from "@/services/apiClient";
import { CHAT_FAILURES, type ChatFailure } from "@/services/types";

export interface LocalizedError {
  key: MessageKey;
  params?: MessageParams;
}

function isChatFailure(value: unknown): value is ChatFailure {
  return typeof value === "string" && (CHAT_FAILURES as readonly string[]).includes(value);
}

/**
 * The text to show for a failed chat call. The API's `failure` code decides it, never its English `detail`, so the
 * message follows the selected language.
 */
export function chatErrorMessage(error: unknown): LocalizedError {
  if (error instanceof ApiError) {
    if (error.isNetworkError) return { key: "error.network" };
    const failure = error.problem?.failure;
    if (isChatFailure(failure)) return { key: `error.${failure}` };
    if (error.status === 400) return { key: "error.validation" };
    return { key: "error.unknown", params: { status: error.status } };
  }
  return { key: "error.network" };
}
