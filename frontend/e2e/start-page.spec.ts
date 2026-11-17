import { expect, test, type Page } from "@playwright/test";
import { expectNoA11yViolations } from "./a11y";

const navigation = (page: Page) => page.getByRole("navigation", { name: "Main navigation" });

test.describe("start page", () => {
  test("opens a new chat, with no home page in the navigation", async ({ page }) => {
    await page.goto("/");

    await expect(page).toHaveTitle("Chat · DotNextAI");
    await expect(page.getByRole("heading", { level: 1, name: "Chat" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Ask anything" })).toBeVisible();
    await expect(navigation(page).getByRole("link", { name: "Chat" })).toHaveAttribute("aria-current", "page");
    await expect(navigation(page).getByRole("link", { name: "Home" })).toHaveCount(0);
    await expectNoA11yViolations(page);
  });

  test("redirects the old chat address to the start page", async ({ page }) => {
    await page.goto("/chat");
    await expect(page).toHaveURL(/:\d+\/$/);
    await expect(page.getByRole("heading", { name: "Ask anything" })).toBeVisible();

    // The saved chat in the old address is kept by the redirect (this one no longer exists).
    await page.goto("/chat?c=00000000-0000-0000-0000-000000000000");
    await expect(page.getByText("That chat no longer exists. You are in a new conversation.")).toBeVisible();
    await expect(page).toHaveURL(/:\d+\/$/);
  });

  test("goes back to the chat from another page", async ({ page }) => {
    await page.goto("/images");

    await navigation(page).getByRole("link", { name: "Chat" }).click();

    await expect(page).toHaveURL(/:\d+\/$/);
    await expect(page.getByRole("heading", { name: "Ask anything" })).toBeVisible();
  });

  test("collapses the navigation to an icon rail", async ({ page }) => {
    await page.goto("/");

    await page.getByRole("button", { name: "Collapse navigation" }).click();

    await expect(navigation(page)).toHaveAttribute("data-collapsed", "true");
    await expect(navigation(page).getByRole("link", { name: "Chat" })).toBeVisible();
  });
});
