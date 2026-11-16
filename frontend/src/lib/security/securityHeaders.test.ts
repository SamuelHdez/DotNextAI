import { describe, expect, it } from "vitest";
import { securityHeaders } from "@/lib/security/securityHeaders";

const asRecord = (isProduction: boolean) =>
  Object.fromEntries(securityHeaders(isProduction).map(({ key, value }) => [key, value]));

describe("securityHeaders", () => {
  it("sets the baseline hardening headers", () => {
    expect(asRecord(false)).toMatchObject({
      "X-Content-Type-Options": "nosniff",
      "Referrer-Policy": "strict-origin-when-cross-origin",
      "X-Frame-Options": "DENY",
      "Cross-Origin-Opener-Policy": "same-origin",
    });
    expect(asRecord(false)["Permissions-Policy"]).toContain("camera=()");
  });

  it("adds HSTS in production only", () => {
    expect(asRecord(false)).not.toHaveProperty("Strict-Transport-Security");
    expect(asRecord(true)["Strict-Transport-Security"]).toBe("max-age=63072000; includeSubDomains");
  });
});
