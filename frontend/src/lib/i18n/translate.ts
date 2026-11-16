import type { Locale } from "@/lib/i18n/locales";
import { en, type MessageKey, type Messages } from "@/lib/i18n/messages/en";
import { es } from "@/lib/i18n/messages/es";

export type MessageParams = Record<string, string | number>;

const CATALOGS: Record<Locale, Messages> = { en, es };

export function messagesFor(locale: Locale): Messages {
  return CATALOGS[locale];
}

/** Replaces `{name}` placeholders; numbers are formatted for the locale (20,000 / 20.000). Unknown ones stay as is. */
export function translate(locale: Locale, key: MessageKey, params?: MessageParams): string {
  const template = CATALOGS[locale][key];
  if (!params) return template;
  const numbers = new Intl.NumberFormat(locale);
  return template.replace(/\{(\w+)\}/g, (placeholder, name: string) => {
    const value = params[name];
    if (value === undefined) return placeholder;
    return typeof value === "number" ? numbers.format(value) : value;
  });
}
