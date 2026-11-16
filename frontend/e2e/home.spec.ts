import { expect, test } from "@playwright/test";
import { expectNoA11yViolations } from "./a11y";

test.describe("home dashboard", () => {
  test("shows the service status of the real API with the model configured", async ({ page }) => {
    await page.goto("/");

    await expect(page).toHaveTitle("Home · DotNextAI");
    await expect(page.getByRole("heading", { level: 1, name: "Welcome to DotNextAI" })).toBeVisible();
    await expect(page.getByText("Online")).toBeVisible();
    await expect(page.getByText("Configured", { exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Course backlog" })).toBeVisible();
  });

  test("opens the chat from the dashboard and from the navigation", async ({ page }) => {
    await page.goto("/");

    await page.getByRole("link", { name: "Open chat" }).click();
    await expect(page).toHaveURL(/\/chat$/);
    await expect(page.getByRole("heading", { level: 1, name: "Chat" })).toBeVisible();

    await page.getByRole("navigation", { name: "Main navigation" }).getByRole("link", { name: "Home" }).click();
    await expect(page).toHaveURL(/\/$/);
  });

  test("collapses the navigation to an icon rail", async ({ page }) => {
    await page.goto("/");
    const navigation = page.getByRole("navigation", { name: "Main navigation" });

    await page.getByRole("button", { name: "Collapse navigation" }).click();

    await expect(navigation).toHaveAttribute("data-collapsed", "true");
    await expect(navigation.getByRole("link", { name: "Chat" })).toBeVisible();
  });

  test("has no serious accessibility violations", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByText("Online")).toBeVisible();

    await expectNoA11yViolations(page);
  });
});
