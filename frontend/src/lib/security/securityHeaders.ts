export interface HttpHeader {
  key: string;
  value: string;
}

/**
 * Static security headers for every response. The Content-Security-Policy is per request (it carries a nonce),
 * so it is set in proxy.ts instead.
 */
export function securityHeaders(isProduction: boolean): HttpHeader[] {
  return [
    { key: "X-Content-Type-Options", value: "nosniff" },
    { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
    // Legacy counterpart of CSP frame-ancestors 'none'.
    { key: "X-Frame-Options", value: "DENY" },
    { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
    {
      key: "Permissions-Policy",
      value: "camera=(), microphone=(), geolocation=(), payment=(), usb=(), browsing-topics=(), interest-cohort=()",
    },
    // Browsers ignore HSTS over plain HTTP, but keep it out of development to avoid pinning localhost.
    ...(isProduction ? [{ key: "Strict-Transport-Security", value: "max-age=63072000; includeSubDomains" }] : []),
  ];
}
