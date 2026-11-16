import { act, render, renderHook, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { I18nProvider, useI18n } from "@/lib/i18n/I18nProvider";
import { LOCALE_COOKIE } from "@/lib/i18n/locales";

afterEach(() => {
  document.cookie = `${LOCALE_COOKIE}=; max-age=0; path=/`;
  document.documentElement.lang = "";
});

function Greeting() {
  const { t, setLocale } = useI18n();
  return (
    <button type="button" onClick={() => setLocale("es")}>
      {t("nav.home")}
    </button>
  );
}

describe("I18nProvider", () => {
  it("renders the initial locale", () => {
    render(
      <I18nProvider initialLocale="es">
        <Greeting />
      </I18nProvider>,
    );

    expect(screen.getByRole("button")).toHaveTextContent("Inicio");
  });

  it("switching the locale updates the texts, the cookie and the document language", () => {
    render(
      <I18nProvider initialLocale="en">
        <Greeting />
      </I18nProvider>,
    );

    act(() => screen.getByRole("button").click());

    expect(screen.getByRole("button")).toHaveTextContent("Inicio");
    expect(document.cookie).toContain(`${LOCALE_COOKIE}=es`);
    expect(document.documentElement.lang).toBe("es");
  });

  it("fails loudly outside the provider", () => {
    expect(() => renderHook(() => useI18n())).toThrow("useI18n must be used inside <I18nProvider>.");
  });
});
