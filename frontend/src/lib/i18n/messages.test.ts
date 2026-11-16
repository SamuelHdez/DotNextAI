import { describe, expect, it } from "vitest";
import { en } from "@/lib/i18n/messages/en";
import { es } from "@/lib/i18n/messages/es";

const placeholders = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort();

describe("message catalogs", () => {
  it("Spanish has exactly the English keys", () => {
    expect(Object.keys(es).sort()).toEqual(Object.keys(en).sort());
  });

  it.each(Object.keys(en) as (keyof typeof en)[])("%s uses the same placeholders in both languages", (key) => {
    expect(placeholders(es[key])).toEqual(placeholders(en[key]));
  });

  it("has no empty texts", () => {
    for (const catalog of [en, es]) {
      expect(Object.values(catalog).filter((text) => text.trim() === "")).toEqual([]);
    }
  });
});
