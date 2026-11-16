import { createDOMRenderer } from "@fluentui/react-components";
import { afterEach, describe, expect, it } from "vitest";
import { adoptStreamedStyles, RULE_DELIMITER, STREAM_BUCKET_ATTR } from "@/lib/griffel/streamedStyles";

function streamedStyle(bucket: string, rules: string[], attributes: Record<string, string> = {}) {
  const element = document.createElement("style");
  element.setAttribute(STREAM_BUCKET_ATTR, bucket);
  element.setAttribute("data-priority", "0");
  for (const [name, value] of Object.entries(attributes)) element.setAttribute(name, value);
  element.textContent = rules.join(RULE_DELIMITER);
  document.body.append(element);
  return element;
}

afterEach(() => {
  document.head.innerHTML = "";
  document.body.innerHTML = "";
});

describe("adoptStreamedStyles (client)", () => {
  it("moves streamed rules into Griffel's <head> buckets and removes the body copies", () => {
    const renderer = createDOMRenderer(document);
    const element = streamedStyle("d", [".b{color:green}", ".c{color:black}"]);

    expect(adoptStreamedStyles(renderer)).toBe(1);

    expect(element.isConnected).toBe(false);
    expect(document.body.querySelector("style")).toBeNull();
    expect(renderer.insertionCache).toMatchObject({ ".b{color:green}": "d", ".c{color:black}": "d" });
    const head = document.head.querySelector<HTMLStyleElement>('style[data-make-styles-bucket="d"]');
    expect(head?.sheet?.cssRules).toHaveLength(2);
  });

  it("keeps media queries in their own bucket and orders buckets in <head>", () => {
    const renderer = createDOMRenderer(document);
    streamedStyle("m", ["@media (min-width: 640px){.m{color:blue}}"], { media: "(min-width: 640px)" });
    streamedStyle("r", [".reset{display:flex}"]);

    adoptStreamedStyles(renderer);

    const buckets = Array.from(document.head.querySelectorAll("style"), (style) => style.dataset.makeStylesBucket);
    expect(buckets).toEqual(["r", "m"]);
    expect(document.head.querySelector('style[data-make-styles-bucket="m"]')?.getAttribute("media")).toBe("(min-width: 640px)");
  });

  it("does not insert a rule Griffel already has", () => {
    const renderer = createDOMRenderer(document);
    renderer.insertCSSRules({ d: [".a{color:red}"] });
    streamedStyle("d", [".a{color:red}", ".b{color:green}"]);

    adoptStreamedStyles(renderer);

    expect(document.head.querySelector<HTMLStyleElement>('style[data-make-styles-bucket="d"]')?.sheet?.cssRules).toHaveLength(2);
  });
});
