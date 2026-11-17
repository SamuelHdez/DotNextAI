import { describe, expect, it } from "vitest";
import { formatCost } from "@/lib/chat/cost";

describe("formatCost", () => {
  it.each([
    [0.000137, "$0.00014"],
    [0.0003, "$0.00030"],
    [0.0123, "$0.012"],
    [0.25, "$0.25"],
    [0.5, "$0.50"],
    [1.234, "$1.23"],
    [12, "$12.00"],
    [0, "$0.00"],
  ])("%s is %s", (cost, expected) => {
    expect(formatCost(cost, "en")).toBe(expected);
  });

  it("follows the UI language", () => {
    expect(formatCost(0.000137, "es").replace(/\s/g, " ")).toBe("0,00014 US$");
  });
});
