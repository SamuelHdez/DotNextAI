import { expect, test } from "@playwright/test";
import { expectNoA11yViolations } from "./a11y";

test.describe("preferences", () => {
  test("switches to Spanish and keeps it after a reload", async ({ page }) => {
    await page.goto("/");

    await page.getByRole("button", { name: "EN · Language: English" }).click();
    await page.getByRole("menuitemradio", { name: "Español" }).click();

    await expect(page.getByRole("heading", { level: 1, name: "Te damos la bienvenida a DotNextAI" })).toBeVisible();
    await expect(page.locator("html")).toHaveAttribute("lang", "es");

    await page.reload();

    // Rendered in Spanish by the server, from the cookie.
    await expect(page).toHaveTitle("Inicio · DotNextAI");
    await expect(page.locator("html")).toHaveAttribute("lang", "es");
    await expect(page.getByRole("navigation", { name: "Navegación principal" })).toBeVisible();
  });

  test("keeps the chat when the language changes", async ({ page }) => {
    await page.goto("/chat");
    const input = page.getByRole("textbox", { name: "Your question" });
    await input.fill("Hola");
    await input.press("Enter");
    await expect(page.getByText("Echo: Hola")).toBeVisible();

    await page.getByRole("button", { name: "EN · Language: English" }).click();
    await page.getByRole("menuitemradio", { name: "Español" }).click();

    await expect(page.getByRole("textbox", { name: "Tu pregunta" })).toBeVisible();
    await expect(page.getByText("Echo: Hola")).toBeVisible();
  });

  test("switches to the dark theme and keeps it after a reload", async ({ page }) => {
    await page.goto("/");

    await page.getByRole("button", { name: "Appearance: System" }).click();
    await page.getByRole("menuitemradio", { name: "Dark" }).click();
    await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");

    await page.reload();

    await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
    await expect(page.getByText("Online")).toBeVisible();
    await expectNoA11yViolations(page);
  });
});
