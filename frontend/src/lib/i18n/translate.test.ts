import { describe, expect, it } from "vitest";
import { isLocale, resolveLocale } from "@/lib/i18n/locales";
import { translate } from "@/lib/i18n/translate";

describe("translate", () => {
  it("returns the text of the locale", () => {
    expect(translate("en", "nav.images")).toBe("Images");
    expect(translate("es", "nav.images")).toBe("Imágenes");
  });

  it("fills placeholders and formats numbers for the locale", () => {
    expect(translate("en", "question.tooLong", { count: 20001, max: 20000 })).toBe(
      "The question is too long (20,001 characters; the maximum is 20,000).",
    );
    expect(translate("es", "chat.input.counter", { count: 16500, max: 20000 })).toBe("16.500 / 20.000");
  });

  it("leaves unknown placeholders untouched", () => {
    expect(translate("en", "error.unknown")).toBe("Something went wrong (HTTP {status}). Try again.");
  });
});

describe("locales", () => {
  it("resolves the cookie value, defaulting to English", () => {
    expect(resolveLocale("es")).toBe("es");
    expect(resolveLocale("en")).toBe("en");
    expect(resolveLocale("fr")).toBe("en");
    expect(resolveLocale(undefined)).toBe("en");
    expect(isLocale("ES")).toBe(false);
  });
});
