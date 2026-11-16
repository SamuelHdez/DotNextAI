// @vitest-environment node
import { describe, expect, it } from "vitest";
import { ERNI_COLORS, erniBrandRamp } from "@/lib/theme/erniBrand";
import { erniDarkTheme, erniLightTheme } from "@/lib/theme/fluentThemeCss";

function luminance(hex: string): number {
  const channel = (offset: number) => {
    const value = parseInt(hex.slice(offset, offset + 2), 16) / 255;
    return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * channel(1) + 0.7152 * channel(3) + 0.0722 * channel(5);
}

function contrast(foreground: string, background: string): number {
  const [light, dark] = [luminance(foreground), luminance(background)].sort((a, b) => b - a);
  return (light + 0.05) / (dark + 0.05);
}

const AA_TEXT = 4.5;
const AA_UI = 3;

describe("ERNI brand ramp", () => {
  it("anchors shade 80 (primary actions) on ERNI Dark Blue from the Brand Book", () => {
    expect(erniBrandRamp[80]).toBe("#033778");
  });

  it("goes monotonically from dark to light", () => {
    const shades = Object.values(erniBrandRamp).map(luminance);
    expect(shades).toEqual([...shades].sort((a, b) => a - b));
  });
});

describe("WCAG AA contrast", () => {
  const light = erniLightTheme;
  const dark = erniDarkTheme;

  it.each([
    ["light: primary button text", light.colorNeutralForegroundOnBrand, light.colorBrandBackground, AA_TEXT],
    ["light: body text", light.colorNeutralForeground1, light.colorNeutralBackground1, AA_TEXT],
    ["light: links", light.colorBrandForegroundLink, light.colorNeutralBackground1, AA_TEXT],
    ["light: brand foreground", light.colorBrandForeground1, light.colorNeutralBackground1, AA_TEXT],
    ["dark: primary button text", dark.colorNeutralForegroundOnBrand, dark.colorBrandBackground, AA_TEXT],
    ["dark: body text", dark.colorNeutralForeground1, dark.colorNeutralBackground1, AA_TEXT],
    ["dark: links", dark.colorBrandForegroundLink, dark.colorNeutralBackground1, AA_TEXT],
    ["dark: brand foreground", dark.colorBrandForeground1, dark.colorNeutralBackground1, AA_TEXT],
    ["dark: checkbox / focus accents", dark.colorCompoundBrandForeground1, dark.colorNeutralBackground1, AA_UI],
    ["sign-in panel: white on ERNI Dark Blue", "#ffffff", ERNI_COLORS.darkBlue, AA_TEXT],
  ])("%s", (_, foreground, background, minimum) => {
    expect(contrast(foreground, background)).toBeGreaterThanOrEqual(minimum);
  });
});
