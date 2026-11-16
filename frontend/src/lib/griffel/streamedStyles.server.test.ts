// @vitest-environment node
import { createDOMRenderer } from "@fluentui/react-components";
import { describe, expect, it } from "vitest";
import { RULE_DELIMITER, snapshotEmittedRules, STREAM_BUCKET_ATTR, takeNewRules } from "@/lib/griffel/streamedStyles";

describe("takeNewRules (server)", () => {
  it("returns only rules added since the last flush, in Griffel bucket order", () => {
    const renderer = createDOMRenderer();
    renderer.insertCSSRules({ d: [".a{color:red}"] });
    const emitted = snapshotEmittedRules(renderer);

    renderer.insertCSSRules({
      m: [["@media (min-width: 640px){.m{color:blue}}", { m: "(min-width: 640px)" }]],
      d: [".b{color:green}", ".c{color:black}"],
      r: [".reset{display:flex}"],
    });
    const streamed = takeNewRules(renderer, emitted);

    expect(streamed.map((style) => style.attributes[STREAM_BUCKET_ATTR])).toEqual(["r", "d", "m"]);
    expect(streamed[1].css).toBe([".b{color:green}", ".c{color:black}"].join(RULE_DELIMITER));
    expect(streamed[2].attributes).toMatchObject({ media: "(min-width: 640px)", "data-priority": "0" });
    // Griffel's own bucket attribute is replaced, so client rehydration does not treat these as server sheets.
    expect(streamed.every((style) => !("data-make-styles-bucket" in style.attributes))).toBe(true);
  });

  it("returns nothing when no rule was added, and never resends a rule", () => {
    const renderer = createDOMRenderer();
    renderer.insertCSSRules({ d: [".a{color:red}"] });
    const emitted = snapshotEmittedRules(renderer);

    expect(takeNewRules(renderer, emitted)).toEqual([]);
    renderer.insertCSSRules({ d: [".b{color:green}"] });
    expect(takeNewRules(renderer, emitted)).toHaveLength(1);
    expect(takeNewRules(renderer, emitted)).toEqual([]);
  });
});
