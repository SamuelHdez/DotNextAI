import { expect, test } from "@playwright/test";
import { expectNoA11yViolations } from "./a11y";

test.describe("images", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/images");
    await expect(page.getByRole("heading", { level: 1, name: "Images" })).toBeVisible();
  });

  test("generates an image through the API and the proxy, ready to download", async ({ page }) => {
    await page.getByRole("textbox", { name: "Description" }).fill("A fox reading");
    await page.getByRole("radio", { name: "Portrait" }).check();
    await page.getByRole("button", { name: "Generate" }).click();

    const gallery = page.getByRole("list", { name: "Images" });
    await expect(gallery.getByRole("img", { name: "Generated image: A fox reading" })).toBeVisible();
    await expect(gallery.getByText("The model drew: Drawn: A fox reading (1024x1536)")).toBeVisible();
    await expect(gallery.getByText("gpt-image-2")).toBeVisible();
    await expect(gallery.getByText("4,164 tokens (4 in · 4,160 out)")).toBeVisible();

    const downloadPromise = page.waitForEvent("download");
    await gallery.getByRole("link", { name: "Download" }).click();
    expect((await downloadPromise).suggestedFilename()).toBe("dotnextai-image-1.png");

    await expectNoA11yViolations(page);
  });

  test("explains an outage of the image model", async ({ page }) => {
    await page.getByRole("textbox", { name: "Description" }).fill("Anything [e2e:unavailable]");
    await page.getByRole("button", { name: "Generate" }).click();

    await expect(page.getByText("The model is temporarily unavailable. Try again in a few minutes.")).toBeVisible();
  });

  test("is reachable from the navigation", async ({ page }) => {
    await page.goto("/");

    await page.getByRole("navigation", { name: "Main navigation" }).getByRole("link", { name: "Images" }).click();

    await expect(page).toHaveURL(/\/images$/);
  });
});
