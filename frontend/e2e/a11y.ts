import AxeBuilder from "@axe-core/playwright";
import { expect, type Page } from "@playwright/test";

/** No WCAG 2.2 A/AA violation of any impact on the current page (same rule set as the ctf-janus admin suite). */
export async function expectNoA11yViolations(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"])
    // Tabster (Fluent UI's focus manager) adds focusable aria-hidden <i> sentinels that redirect focus at once.
    .exclude("[data-tabster-dummy]")
    .analyze();
  const violations = results.violations.map(
    (violation) => `${violation.id}: ${violation.help} → ${violation.nodes.map((node) => `${node.target.join(" ")} ${node.html}`).join(", ")}`,
  );
  expect(violations).toEqual([]);
}
