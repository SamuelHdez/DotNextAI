import { FluentProvider, webLightTheme } from "@fluentui/react-components";
import { render, type RenderOptions } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import { I18nProvider } from "@/lib/i18n/I18nProvider";
import type { Locale } from "@/lib/i18n/locales";

/** Renders inside Fluent UI and the i18n provider, as the app does. */
export function renderWithProviders(ui: ReactElement, { locale = "en", ...options }: { locale?: Locale } & RenderOptions = {}) {
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <FluentProvider theme={webLightTheme}>
        <I18nProvider initialLocale={locale}>{children}</I18nProvider>
      </FluentProvider>
    );
  }
  return render(ui, { wrapper: Wrapper, ...options });
}

/** A fetch Response with a JSON body. */
export function jsonResponse(body: unknown, status = 200, contentType = "application/json"): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": contentType } });
}
