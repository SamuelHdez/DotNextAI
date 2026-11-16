import { NextResponse, type NextRequest } from "next/server";
import { buildContentSecurityPolicy, createNonce, CSP_HEADER, NONCE_HEADER } from "@/lib/security/contentSecurityPolicy";

/**
 * Issues the per-request Content-Security-Policy. Next.js reads the nonce from the CSP request header while rendering
 * and adds it to its scripts; `x-nonce` exposes it to our own inline script in the root layout.
 * There is no authentication: the app is for local use, like the API.
 */
export function proxy(request: NextRequest) {
  const nonce = createNonce();
  const policy = buildContentSecurityPolicy({
    nonce,
    isDevelopment: process.env.NODE_ENV === "development",
    apiBaseUrl: process.env.NEXT_PUBLIC_API_BASE_URL,
  });

  const forwardedHeaders = new Headers(request.headers);
  forwardedHeaders.set(NONCE_HEADER, nonce);
  forwardedHeaders.set(CSP_HEADER, policy);

  const response = NextResponse.next({ request: { headers: forwardedHeaders } });
  response.headers.set(CSP_HEADER, policy);
  return response;
}

export const config = {
  matcher: [
    // Everything except Next internals and static files.
    "/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp|ico)$).*)",
  ],
};
