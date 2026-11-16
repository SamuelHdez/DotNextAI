import { screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { SideNav, selectedValueFor } from "@/components/shell/SideNav";
import { renderWithProviders } from "@/test/render";

vi.mock("next/navigation", () => ({
  usePathname: () => "/chat",
  useRouter: () => ({ push: vi.fn() }),
}));

describe("selectedValueFor", () => {
  it.each([
    ["/", "home"],
    ["/chat", "chat"],
    ["/chat/anything", "chat"],
    ["/images", "images"],
    ["/unknown", ""],
  ])("%s selects %j", (pathname, expected) => {
    expect(selectedValueFor(pathname)).toBe(expected);
  });
});

describe("SideNav", () => {
  it("links every page with localized labels and marks the current one", () => {
    renderWithProviders(<SideNav type="inline" open />, { locale: "es" });

    expect(screen.getByRole("navigation", { name: "Navegación principal" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Inicio" })).toHaveAttribute("href", "/");
    expect(screen.getByRole("link", { name: "Chat" })).toHaveAttribute("aria-current", "page");
  });

  it("keeps accessible names in the collapsed rail", () => {
    renderWithProviders(<SideNav type="inline" open collapsed />);

    expect(screen.getByRole("link", { name: "Home" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Chat" })).toBeInTheDocument();
  });
});
