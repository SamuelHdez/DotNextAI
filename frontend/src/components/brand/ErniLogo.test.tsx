import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ErniLogo } from "@/components/brand/ErniLogo";

describe("ErniLogo", () => {
  it("renders the full logo for light surfaces and the white coat of arms for dark ones", () => {
    render(<ErniLogo height={24} />);

    const [full, coatOfArms] = screen.getAllByAltText("ERNI");
    expect(full).toHaveAttribute("src", "/brand/erni-logo.svg");
    expect(full).toHaveClass("erni-logo-light-surface");
    expect(coatOfArms).toHaveAttribute("src", "/brand/erni-coat-of-arms-white.svg");
    expect(coatOfArms).toHaveClass("erni-logo-dark-surface");
  });

  it("respects the Brand Book minimum digital height of 24px", () => {
    render(<ErniLogo height={24} />);

    for (const logo of screen.getAllByAltText("ERNI")) {
      expect(Number(logo.getAttribute("height"))).toBeGreaterThanOrEqual(24);
    }
  });

  it("only shows the white coat of arms on dark brand panels", () => {
    render(<ErniLogo surface="on-dark" height={32} />);

    const logo = screen.getByAltText("ERNI");
    expect(logo).toHaveAttribute("src", "/brand/erni-coat-of-arms-white.svg");
    expect(logo).not.toHaveClass("erni-logo-dark-surface");
  });
});
