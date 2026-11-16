import type { MessageKey } from "@/lib/i18n/messages/en";
import type { MessageParams } from "@/lib/i18n/translate";
import { ApiError } from "@/services/apiClient";
import { CHAT_FAILURES, STORE_FAILURES, type ChatFailure, type StoreFailure } from "@/services/types";

export interface LocalizedError {
  key: MessageKey;
  params?: MessageParams;
}

const FAILURES: readonly string[] = [...CHAT_FAILURES, ...STORE_FAILURES];

function isKnownFailure(value: unknown): value is ChatFailure | StoreFailure {
  return typeof value === "string" && FAILURES.includes(value);
}

/**
 * The text to show for a failed chat or saved chats call. The API's `failure` code decides it, never its English `detail`, so the
 * message follows the selected language.
 */
export function chatErrorMessage(error: unknown): LocalizedError {
  if (error instanceof ApiError) {
    if (error.isNetworkError) return { key: "error.network" };
    const failure = error.problem?.failure;
    if (isKnownFailure(failure)) return { key: `error.${failure}` };
    if (error.status === 400) return { key: "error.validation" };
    return { key: "error.unknown", params: { status: error.status } };
  }
  return { key: "error.network" };
}
