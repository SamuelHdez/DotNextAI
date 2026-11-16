import { cookies } from "next/headers";
import { LOCALE_COOKIE, resolveLocale, type Locale } from "@/lib/i18n/locales";

/** Language of the current request (Server Components and metadata). */
export async function getRequestLocale(): Promise<Locale> {
  return resolveLocale((await cookies()).get(LOCALE_COOKIE)?.value);
}
