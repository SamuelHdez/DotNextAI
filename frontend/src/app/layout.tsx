import type { Metadata } from "next";
import { Source_Sans_3 } from "next/font/google";
import { headers } from "next/headers";
import { Providers } from "@/components/Providers";
import { getRequestLocale } from "@/lib/i18n/server";
import { translate } from "@/lib/i18n/translate";
import { NONCE_HEADER } from "@/lib/security/contentSecurityPolicy";
import { fluentThemeCss } from "@/lib/theme/fluentThemeCss";
import { themeBootstrapScript } from "@/lib/theme/themeBootstrap";
import "./globals.css";

// ERNI primary typeface (Brand Book: Source Sans Pro Light / Regular / SemiBold), self-hosted by next/font.
const erniSans = Source_Sans_3({
  subsets: ["latin", "latin-ext"],
  weight: ["300", "400", "600", "700"],
  variable: "--font-erni-sans",
  display: "swap",
});

export async function generateMetadata(): Promise<Metadata> {
  const locale = await getRequestLocale();
  return {
    title: { default: translate(locale, "app.name"), template: `%s · ${translate(locale, "app.name")}` },
    description: translate(locale, "app.description"),
  };
}

export default async function RootLayout({ children }: LayoutProps<"/">) {
  // Set per request by proxy.ts; reading it also keeps every page dynamically rendered, as the nonce requires.
  const nonce = (await headers()).get(NONCE_HEADER) ?? undefined;
  const locale = await getRequestLocale();

  return (
    // The bootstrap script sets data-theme before hydration, so React must not treat it as a mismatch.
    <html lang={locale} className={erniSans.variable} suppressHydrationWarning>
      <head>
        <script id="theme-bootstrap" nonce={nonce} dangerouslySetInnerHTML={{ __html: themeBootstrapScript }} />
        <style id="fluent-theme" dangerouslySetInnerHTML={{ __html: fluentThemeCss }} />
      </head>
      <body>
        <Providers locale={locale}>{children}</Providers>
      </body>
    </html>
  );
}
