/** Same rules as the backend's ImageFormats and ChatRules (docs/specs/feature-llm-chat.md#images). */
export const SUPPORTED_IMAGE_TYPES = ["image/png", "image/jpeg", "image/webp", "image/gif"] as const;
export type ImageMediaType = (typeof SUPPORTED_IMAGE_TYPES)[number];

export const MAX_IMAGES_PER_QUESTION = 4;
export const MAX_IMAGE_BYTES = 4 * 1024 * 1024;
export const MAX_IMAGE_MEGABYTES = MAX_IMAGE_BYTES / 1024 / 1024;

/** An image waiting to be sent: the base64 bytes for the API and a data URI for the preview. */
export interface ImageAttachment {
  id: string;
  name: string;
  mediaType: ImageMediaType;
  /** File bytes in base64, without the `data:` prefix (what POST /api/chat expects). */
  data: string;
  /** `data:` URI for <img> (allowed by the CSP's img-src). */
  previewUrl: string;
}

const startsWith = (bytes: Uint8Array, signature: number[], offset = 0) => signature.every((byte, i) => bytes[offset + i] === byte);
const ascii = (text: string) => [...text].map((char) => char.charCodeAt(0));

/** The format from the file signature ("magic bytes"), never from the name or the browser's guess. */
export function detectImageType(bytes: Uint8Array): ImageMediaType | null {
  if (startsWith(bytes, [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])) return "image/png";
  if (startsWith(bytes, [0xff, 0xd8, 0xff])) return "image/jpeg";
  if (startsWith(bytes, ascii("GIF87a")) || startsWith(bytes, ascii("GIF89a"))) return "image/gif";
  if (bytes.length >= 12 && startsWith(bytes, ascii("RIFF")) && startsWith(bytes, ascii("WEBP"), 8)) return "image/webp";
  return null;
}

function toBase64(bytes: Uint8Array): string {
  // Chunked: String.fromCharCode(...bytes) on a whole image would overflow the call stack.
  let binary = "";
  for (let i = 0; i < bytes.length; i += 0x8000) binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  return btoa(binary);
}

export type ImageReadResult =
  | { ok: true; attachment: Omit<ImageAttachment, "id"> }
  | { ok: false; reason: "tooLarge" | "unsupported" | "unreadable"; name: string };

/** Reads a picked or pasted file into an attachment, or says why it cannot be sent. */
export async function readImageFile(file: File): Promise<ImageReadResult> {
  const name = file.name || "image";
  // Checked before reading, so a huge file is never loaded.
  if (file.size > MAX_IMAGE_BYTES) return { ok: false, reason: "tooLarge", name };
  let bytes: Uint8Array;
  try {
    bytes = new Uint8Array(await file.arrayBuffer());
  } catch {
    return { ok: false, reason: "unreadable", name };
  }
  const mediaType = detectImageType(bytes);
  if (!mediaType) return { ok: false, reason: "unsupported", name };
  const data = toBase64(bytes);
  return { ok: true, attachment: { name, mediaType, data, previewUrl: `data:${mediaType};base64,${data}` } };
}
