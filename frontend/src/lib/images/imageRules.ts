/** Same limit as the backend's ImageRules (docs/specs/feature-images.md). */
export const MAX_PROMPT_LENGTH = 4_000;

export type PromptCheck = { ok: true; prompt: string } | { ok: false; reason: "empty" } | { ok: false; reason: "tooLong"; length: number };

export function checkPrompt(text: string): PromptCheck {
  const prompt = text.trim();
  if (prompt.length === 0) return { ok: false, reason: "empty" };
  if (prompt.length > MAX_PROMPT_LENGTH) return { ok: false, reason: "tooLong", length: prompt.length };
  return { ok: true, prompt };
}

/** File name for a download: dotnextai-image-<n>.<ext>, the extension from the media type. */
export function downloadName(index: number, mediaType: string): string {
  const extension = { "image/jpeg": "jpg", "image/webp": "webp", "image/gif": "gif" }[mediaType] ?? "png";
  return `dotnextai-image-${index}.${extension}`;
}
