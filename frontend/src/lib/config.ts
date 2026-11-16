/** Public configuration, inlined into the client bundle at build time (NEXT_PUBLIC_*). */
export const appConfig = {
  /** Base URL of DotNextAI.Api, e.g. http://localhost:5100. */
  apiBaseUrl: process.env.NEXT_PUBLIC_API_BASE_URL ?? "",
} as const;
