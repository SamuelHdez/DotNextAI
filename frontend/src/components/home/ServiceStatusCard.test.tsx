import { screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ServiceStatusCard } from "@/components/home/ServiceStatusCard";
import { jsonResponse, renderWithProviders } from "@/test/render";

afterEach(() => vi.unstubAllGlobals());

function health(llmConfigured: boolean) {
  return jsonResponse({ status: "Healthy", service: "DotNextAI.Api", environment: "Development", llmConfigured, timestampUtc: "2026-11-16T00:00:00Z" });
}

describe("ServiceStatusCard", () => {
  it("shows the API online and the model configured", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(health(true)));

    renderWithProviders(<ServiceStatusCard />);

    expect(await screen.findByText("Online")).toBeInTheDocument();
    expect(screen.getByText("Configured")).toBeInTheDocument();
  });

  it("explains how to configure a missing model", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(health(false)));

    renderWithProviders(<ServiceStatusCard />);

    expect(await screen.findByText("Not configured")).toBeInTheDocument();
    expect(screen.getByText("Set the LiteLLM proxy with ./scripts/dev.sh secrets.")).toBeInTheDocument();
  });

  it("explains how to start an unreachable API", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    renderWithProviders(<ServiceStatusCard />, { locale: "es" });

    expect(await screen.findByText("Sin conexión")).toBeInTheDocument();
    expect(screen.getByText("Arranca el backend con ./scripts/dev.sh.")).toBeInTheDocument();
  });
});
