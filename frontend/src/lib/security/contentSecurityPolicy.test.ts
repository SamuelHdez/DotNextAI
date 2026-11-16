import { describe, expect, it } from "vitest";
import { buildContentSecurityPolicy, createNonce } from "@/lib/security/contentSecurityPolicy";

function directives(policy: string): Map<string, string[]> {
  return new Map(
    policy.split(";").map((part) => {
      const [name, ...sources] = part.trim().split(/\s+/);
      return [name, sources];
    }),
  );
}

describe("content security policy", () => {
  it("allows scripts only with the nonce (no unsafe-inline)", () => {
    const policy = directives(buildContentSecurityPolicy({ nonce: "abc", isDevelopment: false }));

    expect(policy.get("script-src")).toEqual(["'self'", "'nonce-abc'", "'strict-dynamic'"]);
    expect(policy.get("script-src")).not.toContain("'unsafe-inline'");
  });

  it("adds unsafe-eval in development only", () => {
    expect(directives(buildContentSecurityPolicy({ nonce: "abc", isDevelopment: true })).get("script-src")).toContain("'unsafe-eval'");
    expect(directives(buildContentSecurityPolicy({ nonce: "abc", isDevelopment: false })).get("script-src")).not.toContain(
      "'unsafe-eval'",
    );
  });

  it("lets the browser connect only to itself and the API origin", () => {
    const policy = directives(
      buildContentSecurityPolicy({ nonce: "abc", isDevelopment: false, apiBaseUrl: "http://localhost:5100/some/path" }),
    );

    expect(policy.get("connect-src")).toEqual(["'self'", "http://localhost:5100"]);
  });

  it("loads images only from itself, data/blob URIs and the API (saved chat images)", () => {
    const policy = directives(buildContentSecurityPolicy({ nonce: "abc", isDevelopment: false, apiBaseUrl: "http://localhost:5100" }));

    expect(policy.get("img-src")).toEqual(["'self'", "data:", "blob:", "http://localhost:5100"]);
  });

  it("ignores an invalid API URL", () => {
    const policy = directives(buildContentSecurityPolicy({ nonce: "abc", isDevelopment: false, apiBaseUrl: "not a url" }));

    expect(policy.get("connect-src")).toEqual(["'self'"]);
  });

  it("blocks plugins, framing and base-uri changes", () => {
    const policy = directives(buildContentSecurityPolicy({ nonce: "abc", isDevelopment: false }));

    expect(policy.get("object-src")).toEqual(["'none'"]);
    expect(policy.get("frame-ancestors")).toEqual(["'none'"]);
    expect(policy.get("base-uri")).toEqual(["'none'"]);
  });

  it("creates a different base64 nonce each time", () => {
    const first = createNonce();

    expect(first).toMatch(/^[A-Za-z0-9+/]{22}==$/);
    expect(createNonce()).not.toBe(first);
  });
});
