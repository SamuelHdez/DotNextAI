// @vitest-environment node
import { describe, expect, it } from "vitest";
import { ERNI_COLORS, ERNI_FONT_STACK } from "@/lib/theme/erniBrand";
import { erniDarkTheme, erniLightTheme, fluentThemeCss } from "@/lib/theme/fluentThemeCss";

describe("fluentThemeCss", () => {
  const [lightRule, systemDarkRule, explicitDarkRule] = fluentThemeCss.split(/(?=@media|:root\[data-theme)/);

  it("uses the ERNI light theme by default", () => {
    expect(lightRule).toMatch(/^:root\{color-scheme:light;/);
    expect(lightRule).toContain(`--colorBrandBackground:${ERNI_COLORS.darkBlue};`);
    expect(lightRule).toContain(`--colorNeutralForeground1:${ERNI_COLORS.text};`);
  });

  it("follows the OS dark preference unless the user explicitly chose light", () => {
    expect(systemDarkRule).toContain('@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){color-scheme:dark;');
    expect(systemDarkRule).toContain(`--colorBrandBackground:${erniDarkTheme.colorBrandBackground};`);
  });

  it("applies the dark theme when the user explicitly chose dark", () => {
    expect(explicitDarkRule).toMatch(/^:root\[data-theme="dark"\]\{color-scheme:dark;/);
  });

  it("emits every Fluent token and uses the ERNI typeface in both themes", () => {
    expect(lightRule.match(/--[a-zA-Z0-9]+:/g)).toHaveLength(Object.keys(erniLightTheme).length);
    expect(erniLightTheme.fontFamilyBase).toBe(ERNI_FONT_STACK);
    expect(erniDarkTheme.fontFamilyBase).toBe(ERNI_FONT_STACK);
  });
});
