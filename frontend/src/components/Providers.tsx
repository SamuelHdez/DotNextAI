"use client";

import {
  createDOMRenderer,
  FluentProvider,
  makeStyles,
  RendererProvider,
  renderToStyleElements,
  SSRProvider,
  tokens,
} from "@fluentui/react-components";
import { useServerInsertedHTML } from "next/navigation";
import { useLayoutEffect, useRef, useState, type ReactNode } from "react";
import {
  adoptStreamedStyles,
  snapshotEmittedRules,
  STREAM_BUCKET_ATTR,
  takeNewRules,
  type EmittedRules,
} from "@/lib/griffel/streamedStyles";
import { I18nProvider } from "@/lib/i18n/I18nProvider";
import type { Locale } from "@/lib/i18n/locales";

const useStyles = makeStyles({
  // Layout lives on an inner element: Fluent copies FluentProvider's own classes onto portal mount nodes
  // (menus, popovers), so styling the provider itself would turn every portal into a full-page layer.
  app: {
    display: "flex",
    flexDirection: "column",
    flexGrow: 1,
    minHeight: "100dvh",
    backgroundColor: tokens.colorNeutralBackground2,
  },
});

const NO_THEME_OVERRIDES = {};

type GriffelRenderer = ReturnType<typeof createDOMRenderer>;

function AppSurface({ children }: { children: ReactNode }) {
  const styles = useStyles();
  return <div className={styles.app}>{children}</div>;
}

/**
 * Client side of the streamed styles (see lib/griffel/streamedStyles.ts): moves rules that arrived with streamed
 * content into Griffel's ordered <head> buckets. Rendered inside RendererProvider, so the renderer has already
 * rehydrated the server styles. The observer runs before paint for every chunk streamed after hydration.
 */
function StreamedStylesAdopter({ renderer }: { renderer: GriffelRenderer }) {
  useLayoutEffect(() => {
    adoptStreamedStyles(renderer);
    const observer = new MutationObserver((mutations) => {
      const hasStreamedStyles = mutations.some((mutation) =>
        Array.from(mutation.addedNodes).some((node) => node instanceof HTMLStyleElement && node.hasAttribute(STREAM_BUCKET_ATTR)),
      );
      if (hasStreamedStyles) adoptStreamedStyles(renderer);
    });
    observer.observe(document.body, { childList: true, subtree: true });
    return () => observer.disconnect();
  }, [renderer]);

  return null;
}

export function Providers({ locale, children }: { locale: Locale; children: ReactNode }) {
  const [renderer] = useState(() => createDOMRenderer());
  const emittedRules = useRef<EmittedRules | null>(null);

  // Griffel (Fluent's CSS-in-JS) collects styles during the server render. The first flush (which lands in <head>)
  // carries the full stylesheet; later flushes only the rules added by content streamed inside <Suspense>.
  useServerInsertedHTML(() => {
    if (!emittedRules.current) {
      emittedRules.current = snapshotEmittedRules(renderer);
      return <>{renderToStyleElements(renderer)}</>;
    }
    const streamed = takeNewRules(renderer, emittedRules.current);
    if (streamed.length === 0) return null;
    return (
      <>
        {streamed.map(({ key, attributes, css }) => (
          <style key={key} {...attributes} dangerouslySetInnerHTML={{ __html: css }} />
        ))}
      </>
    );
  });

  return (
    <RendererProvider renderer={renderer}>
      <StreamedStylesAdopter renderer={renderer} />
      <SSRProvider>
        {/* Tokens come from the CSS variables emitted in <head> (fluentThemeCss.ts); the empty theme only
            tells Fluent that this is intentional, without overriding any variable. */}
        <FluentProvider theme={NO_THEME_OVERRIDES}>
          <I18nProvider initialLocale={locale}>
            <AppSurface>{children}</AppSurface>
          </I18nProvider>
        </FluentProvider>
      </SSRProvider>
    </RendererProvider>
  );
}
