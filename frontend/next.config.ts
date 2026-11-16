import type { NextConfig } from "next";
import { securityHeaders } from "./src/lib/security/securityHeaders";

const nextConfig: NextConfig = {
  output: "standalone",
  poweredByHeader: false,
  // Cache Components / Partial Prerendering stay off: the nonce-based CSP (src/proxy.ts) needs every page to be
  // rendered per request so Next.js can stamp the nonce on its scripts.
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders(process.env.NODE_ENV === "production") }];
  },
};

export default nextConfig;
