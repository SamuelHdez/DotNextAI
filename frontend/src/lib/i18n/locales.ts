/** Server-safe locale constants (used by the root layout, page metadata and the client provider). */
export const LOCALES = ["en", "es"] as const;

export type Locale = (typeof LOCALES)[number];

/** English unless the user picked another language with the selector (docs/specs/feature-web-app.md). */
export const DEFAULT_LOCALE: Locale = "en";

/** Cookie with the chosen language, so the server renders the right language on the first byte. */
export const LOCALE_COOKIE = "dnai-locale";

/** One year: the choice should survive restarts of the browser. */
export const LOCALE_COOKIE_MAX_AGE_SECONDS = 60 * 60 * 24 * 365;

export function isLocale(value: unknown): value is Locale {
  return typeof value === "string" && (LOCALES as readonly string[]).includes(value);
}

export function resolveLocale(cookieValue: string | undefined | null): Locale {
  return isLocale(cookieValue) ? cookieValue : DEFAULT_LOCALE;
}
