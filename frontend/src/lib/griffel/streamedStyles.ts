import type { createDOMRenderer } from "@fluentui/react-components";

/**
 * Streaming-safe server rendering of Griffel (Fluent UI) styles for the App Router.
 *
 * Next.js calls `useServerInsertedHTML` once per streaming flush. Emitting the full stylesheet each time duplicates
 * it into `<body>`, where stale copies can override styles Griffel inserts in `<head>` later. Emitting it only on the
 * first flush (Griffel's guidance) leaves content streamed inside `<Suspense>` unstyled until hydration.
 *
 * So: the first flush emits Griffel's regular `<style>` elements (they land in `<head>`); later flushes emit only the
 * rules added since, in marked `<style>` elements that style the streamed HTML right away. On the client,
 * {@link adoptStreamedStyles} moves those rules into Griffel's ordered `<head>` buckets via the public
 * `insertCSSRules` API and removes the body copies, so every rule exists once, in bucket order.
 * See https://griffel.js.org/react/guides/ssr-usage
 */

type GriffelRenderer = ReturnType<typeof createDOMRenderer>;
type BucketName = keyof Parameters<GriffelRenderer["insertCSSRules"]>[0];

export const STREAM_BUCKET_ATTR = "data-griffel-stream-bucket";

/** Separates rules inside a streamed `<style>`; a CSS comment, so browsers ignore it. */
export const RULE_DELIMITER = "/*griffel-rule*/";

const GRIFFEL_BUCKET_ATTR = "data-make-styles-bucket";
const PRIORITY_ATTR = "data-priority";
const CONTAINER_ATTR = "data-container";

// Mirrors Griffel's `styleBucketOrdering` (not re-exported by @fluentui/react-components).
const BUCKET_ORDER = ["r", "d", "l", "v", "w", "f", "i", "h", "a", "s", "k", "t", "m", "c", "x"];

export interface StreamedStyle {
  key: string;
  attributes: Record<string, string>;
  css: string;
}

/** Number of rules of each stylesheet already sent to the browser. */
export type EmittedRules = Map<string, number>;

export function snapshotEmittedRules(renderer: GriffelRenderer): EmittedRules {
  return new Map(Object.entries(renderer.stylesheets).map(([key, sheet]) => [key, sheet?.cssRules().length ?? 0]));
}

/**
 * Rules added to the server renderer since the last flush, grouped per stylesheet and in Griffel's bucket order.
 * Updates `emitted` so the next flush only sends newer rules.
 */
export function takeNewRules(renderer: GriffelRenderer, emitted: EmittedRules): StreamedStyle[] {
  const chunks: (StreamedStyle & { bucket: string; media: string; priority: number })[] = [];

  for (const [key, sheet] of Object.entries(renderer.stylesheets)) {
    if (!sheet) continue;
    const rules = sheet.cssRules();
    const alreadySent = emitted.get(key) ?? 0;
    if (rules.length <= alreadySent) continue;
    emitted.set(key, rules.length);

    const { [GRIFFEL_BUCKET_ATTR]: bucket = sheet.bucketName, ...rest } = sheet.elementAttributes as Record<string, string>;
    chunks.push({
      key,
      bucket,
      media: rest.media ?? "",
      priority: Number(rest[PRIORITY_ATTR] ?? 0),
      attributes: { ...rest, [STREAM_BUCKET_ATTR]: bucket },
      css: rules.slice(alreadySent).join(RULE_DELIMITER),
    });
  }

  return chunks
    .sort(
      (a, b) =>
        BUCKET_ORDER.indexOf(a.bucket) - BUCKET_ORDER.indexOf(b.bucket) ||
        (a.bucket === "m" ? renderer.compareMediaQueries(a.media, b.media) : 0) ||
        a.priority - b.priority,
    )
    .map(({ key, attributes, css }) => ({ key, attributes, css }));
}

/**
 * Moves rules from streamed `<style>` elements into Griffel's `<head>` buckets and removes the elements.
 * Must run after the renderer rehydrated the server styles (i.e. below `RendererProvider`).
 */
export function adoptStreamedStyles(renderer: GriffelRenderer, root: ParentNode = document): number {
  const elements = root.querySelectorAll<HTMLStyleElement>(`style[${STREAM_BUCKET_ATTR}]`);

  elements.forEach((element) => {
    const bucket = element.getAttribute(STREAM_BUCKET_ATTR) as BucketName;
    const metadata: Record<string, unknown> = {};
    const priority = Number(element.getAttribute(PRIORITY_ATTR) ?? 0);
    if (priority) metadata.p = priority;
    if (bucket === "m" && element.media) metadata.m = element.media;
    const container = element.getAttribute(CONTAINER_ATTR);
    if (bucket === "x" && container) metadata.x = container;

    const hasMetadata = Object.keys(metadata).length > 0;
    const rules = (element.textContent ?? "").split(RULE_DELIMITER).filter(Boolean);
    renderer.insertCSSRules({ [bucket]: rules.map((rule) => (hasMetadata ? ([rule, metadata] as [string, Record<string, unknown>]) : rule)) });
    element.remove();
  });

  return elements.length;
}
