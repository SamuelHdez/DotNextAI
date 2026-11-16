import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { THEME_STORAGE_KEY, themeBootstrapScript } from "@/lib/theme/themeBootstrap";
import { applyThemePreference, readThemePreference, useThemePreference } from "@/lib/theme/themePreference";

afterEach(() => {
  window.localStorage.clear();
  delete document.documentElement.dataset.theme;
});

describe("theme preference", () => {
  it("defaults to the system preference", () => {
    expect(readThemePreference()).toBe("system");
    expect(document.documentElement.dataset.theme).toBeUndefined();
  });

  it.each(["light", "dark"] as const)("stores and applies an explicit %s choice", (preference) => {
    applyThemePreference(preference);

    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(preference);
    expect(document.documentElement.dataset.theme).toBe(preference);
    expect(readThemePreference()).toBe(preference);
  });

  it("going back to system clears the stored choice and the attribute", () => {
    applyThemePreference("dark");

    applyThemePreference("system");

    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBeNull();
    expect(document.documentElement.dataset.theme).toBeUndefined();
  });

  it("ignores unexpected stored values", () => {
    window.localStorage.setItem(THEME_STORAGE_KEY, "sepia");

    expect(readThemePreference()).toBe("system");
  });

  it("useThemePreference re-renders when the preference changes", () => {
    const { result } = renderHook(() => useThemePreference());
    expect(result.current[0]).toBe("system");

    act(() => result.current[1]("dark"));

    expect(result.current[0]).toBe("dark");
  });

  it("follows changes made in another tab", () => {
    const { result } = renderHook(() => useThemePreference());

    act(() => {
      window.localStorage.setItem(THEME_STORAGE_KEY, "light");
      window.dispatchEvent(new StorageEvent("storage", { key: THEME_STORAGE_KEY, newValue: "light" }));
    });

    expect(result.current[0]).toBe("light");
    expect(document.documentElement.dataset.theme).toBe("light");
  });
});

describe("themeBootstrapScript", () => {
  const runBootstrap = () => new Function(themeBootstrapScript)();

  it.each(["light", "dark"])("applies a stored %s choice before React loads", (stored) => {
    window.localStorage.setItem(THEME_STORAGE_KEY, stored);

    runBootstrap();

    expect(document.documentElement.dataset.theme).toBe(stored);
  });

  it("leaves the system preference alone when nothing valid is stored", () => {
    window.localStorage.setItem(THEME_STORAGE_KEY, "<script>");

    runBootstrap();

    expect(document.documentElement.dataset.theme).toBeUndefined();
  });
});
