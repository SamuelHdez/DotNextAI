/** A 1×1 PNG, in base64 and as a File. */
export const PNG_BASE64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

export function pngFile(name = "pixel.png"): File {
  return new File([Uint8Array.from(atob(PNG_BASE64), (char) => char.charCodeAt(0))], name, { type: "image/png" });
}

export function textFile(name = "notes.png"): File {
  return new File(["This is not an image."], name, { type: "image/png" });
}
