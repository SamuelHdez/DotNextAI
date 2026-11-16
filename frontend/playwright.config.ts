import { randomBytes } from "node:crypto";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { defineConfig, devices } from "@playwright/test";

// Own ports, so the suite runs next to ./scripts/dev.sh (5100/3030) without clashing.
const FAKE_LITELLM_PORT = 4010;
const API_PORT = 5199;
const WEB_PORT = 3039;
const API_URL = `http://localhost:${API_PORT}`;
const WEB_URL = `http://localhost:${WEB_PORT}`;
const isCI = Boolean(process.env.CI);

/**
 * Browser tests against the production build of the web app, the real API and a fake LiteLLM proxy
 * (e2e/fake-litellm.mjs): the whole chain runs, without the real proxy, keys or costs (docs/design/testing.md).
 * The API must be built first (`dotnet build backend/DotNextAI.slnx -c Release`; ./scripts/dev.sh e2e does it).
 */
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  forbidOnly: isCI,
  retries: isCI ? 1 : 0,
  reporter: isCI ? [["line"], ["html", { open: "never" }]] : "line",
  use: {
    baseURL: WEB_URL,
    trace: "retain-on-failure",
    reducedMotion: "reduce",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      command: `node e2e/fake-litellm.mjs ${FAKE_LITELLM_PORT}`,
      url: `http://127.0.0.1:${FAKE_LITELLM_PORT}/health`,
      reuseExistingServer: !isCI,
      timeout: 30_000,
    },
    {
      command: "dotnet run --project ../backend/src/DotNextAI.Api --configuration Release --no-build --no-launch-profile",
      url: `${API_URL}/api/health`,
      reuseExistingServer: !isCI,
      timeout: 120_000,
      env: {
        // Not Development: the developer's user-secrets (the real key) are never loaded.
        ASPNETCORE_ENVIRONMENT: "E2E",
        ASPNETCORE_URLS: API_URL,
        Llm__Endpoint: `http://127.0.0.1:${FAKE_LITELLM_PORT}/`,
        // Generated per run: the fake proxy only checks that a key is sent.
        Llm__ApiKey: `e2e-${randomBytes(16).toString("hex")}`,
        Llm__DefaultModel: "e2e-model",
        Llm__ImageModel: "gpt-image-2",
        Llm__MaxRetries: "0",
        Cors__AllowedOrigins__0: WEB_URL,
        RateLimiting__ChatPermitsPerMinute: "1000",
        // A fresh saved-chats database per run, outside the repository.
        ConnectionStrings__Chats: `Data Source=${join(tmpdir(), `dotnextai-e2e-${randomBytes(8).toString("hex")}.db`)}`,
      },
    },
    {
      // Production build, as shipped; NEXT_PUBLIC_API_BASE_URL is inlined at build time.
      command: `npm run build && npm run start -- --port ${WEB_PORT}`,
      url: WEB_URL,
      reuseExistingServer: !isCI,
      timeout: 300_000,
      env: { NEXT_PUBLIC_API_BASE_URL: API_URL, NEXT_TELEMETRY_DISABLED: "1" },
    },
  ],
});
