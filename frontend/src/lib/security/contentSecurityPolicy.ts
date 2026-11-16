export const NONCE_HEADER = "x-nonce";
export const CSP_HEADER = "Content-Security-Policy";

/** 128-bit random nonce, base64-encoded. Must be fresh for every response. */
export function createNonce(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  return btoa(String.fromCharCode(...bytes));
}

function originOf(url: string | undefined): string | undefined {
  if (!url) return undefined;
  try {
    return new URL(url).origin;
  } catch {
    return undefined;
  }
}

interface ContentSecurityPolicyOptions {
  nonce: string;
  isDevelopment: boolean;
  /** Public base URL of DotNextAI.Api, called directly from the browser (health and chat). */
  apiBaseUrl?: string;
}

/**
 * Strict, nonce-based CSP (https://web.dev/articles/strict-csp). Next.js reads the nonce from this header during
 * rendering and adds it to its own scripts; 'strict-dynamic' lets those scripts load the app chunks.
 *
 * Styles keep 'unsafe-inline': Fluent UI (Griffel) inserts <style> elements at runtime and React renders `style`
 * attributes, neither of which can carry a nonce. Script execution, the main XSS vector, stays nonce-only.
 * The browser may only talk to this origin and the API (and load images from them): model answers can never make it load
 * or send anything else.
 */
export function buildContentSecurityPolicy({ nonce, isDevelopment, apiBaseUrl }: ContentSecurityPolicyOptions): string {
  const apiOrigin = originOf(apiBaseUrl);

  const directives: Record<string, string[]> = {
    "default-src": ["'self'"],
    // React needs eval in development only, to rebuild server error stacks in the browser.
    "script-src": ["'self'", `'nonce-${nonce}'`, "'strict-dynamic'", ...(isDevelopment ? ["'unsafe-eval'"] : [])],
    "style-src": ["'self'", "'unsafe-inline'"],
    // The API serves the images saved with a chat (GET /api/conversations/{id}/images/{imageId}).
    "img-src": ["'self'", "data:", "blob:", ...(apiOrigin ? [apiOrigin] : [])],
    "font-src": ["'self'"],
    "connect-src": ["'self'", ...(apiOrigin ? [apiOrigin] : [])],
    "object-src": ["'none'"],
    "base-uri": ["'none'"],
    "form-action": ["'self'"],
    "frame-ancestors": ["'none'"],
  };

  return Object.entries(directives)
    .map(([directive, sources]) => `${directive} ${sources.join(" ")}`)
    .join("; ");
}
