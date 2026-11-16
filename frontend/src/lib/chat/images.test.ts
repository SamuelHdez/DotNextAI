import { describe, expect, it } from "vitest";
import { detectImageType, MAX_IMAGE_BYTES, readImageFile } from "@/lib/chat/images";
import { PNG_BASE64, pngFile, textFile } from "@/test/images";

const bytes = (...values: number[]) => new Uint8Array(values);
const ascii = (text: string) => new TextEncoder().encode(text);

describe("detectImageType", () => {
  it.each([
    ["PNG", bytes(0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0), "image/png"],
    ["JPEG", bytes(0xff, 0xd8, 0xff, 0xe0), "image/jpeg"],
    ["GIF87a", ascii("GIF87a.."), "image/gif"],
    ["GIF89a", ascii("GIF89a.."), "image/gif"],
    ["WebP", ascii("RIFF$\u0000\u0000\u0000WEBPVP8 "), "image/webp"],
  ])("recognises %s by its signature", (_, data, type) => {
    expect(detectImageType(data)).toBe(type);
  });

  it("rejects anything else", () => {
    expect(detectImageType(ascii("This is not an image."))).toBeNull();
    expect(detectImageType(ascii("RIFF....WAVE"))).toBeNull();
    expect(detectImageType(bytes())).toBeNull();
  });
});

describe("readImageFile", () => {
  it("reads an image into base64 data and a data URI preview", async () => {
    const result = await readImageFile(pngFile("cat.png"));

    expect(result).toEqual({
      ok: true,
      attachment: { name: "cat.png", mediaType: "image/png", data: PNG_BASE64, previewUrl: `data:image/png;base64,${PNG_BASE64}` },
    });
  });

  it("uses the content, not the declared type or the name", async () => {
    const result = await readImageFile(new File([pngFile()], "photo.jpg", { type: "image/jpeg" }));

    expect(result.ok && result.attachment.mediaType).toBe("image/png");
  });

  it("rejects a file that is not an image", async () => {
    expect(await readImageFile(textFile("notes.png"))).toEqual({ ok: false, reason: "unsupported", name: "notes.png" });
  });

  it("rejects a file over the size limit without reading it", async () => {
    const big = new File([new Uint8Array(MAX_IMAGE_BYTES + 1)], "big.png", { type: "image/png" });

    expect(await readImageFile(big)).toEqual({ ok: false, reason: "tooLarge", name: "big.png" });
  });
});
