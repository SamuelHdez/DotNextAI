import { expect, test, type Page } from "@playwright/test";
import { expectNoA11yViolations } from "./a11y";

async function ask(page: Page, question: string) {
  const input = page.getByRole("textbox", { name: "Your question" });
  await input.fill(question);
  await input.press("Enter");
}

const conversation = (page: Page) => page.getByRole("log", { name: "Conversation" });

test.describe("chat", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/chat");
    await expect(page.getByRole("heading", { name: "Ask anything" })).toBeVisible();
  });

  test("answers a question through the API, the agent and the proxy", async ({ page }) => {
    await ask(page, "Hello DotNextAI");

    await expect(conversation(page).getByText("Echo: Hello DotNextAI")).toBeVisible();
    await expect(conversation(page).getByText("e2e-model")).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Your question" })).toHaveValue("");
  });

  test("remembers earlier turns of the conversation", async ({ page }) => {
    await ask(page, "My name is Ana.");
    await expect(conversation(page).getByText("Echo: My name is Ana.")).toBeVisible();

    await ask(page, "What is my name?");

    await expect(conversation(page).getByText("Your name is Ana.")).toBeVisible();
  });

  test("asks a suggested question", async ({ page }) => {
    await page.getByRole("button", { name: "Write a haiku about .NET." }).click();

    await expect(conversation(page).getByText("Echo: Write a haiku about .NET.")).toBeVisible();
  });

  test("shows that the model is thinking while it answers", async ({ page }) => {
    await ask(page, "Take your time [e2e:slow]");

    await expect(page.getByText("DotNextAI is thinking…")).toBeVisible();
    await expect(page.getByRole("button", { name: "Send" })).toBeDisabled();
    await expect(conversation(page).getByText("Echo: Take your time [e2e:slow]")).toBeVisible();
    await expect(page.getByText("DotNextAI is thinking…")).toBeHidden();
  });

  test("explains a model outage and gives the question back", async ({ page }) => {
    await ask(page, "Are you there? [e2e:unavailable]");

    await expect(page.getByText("The model is temporarily unavailable. Try again in a few minutes.")).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Your question" })).toHaveValue("Are you there? [e2e:unavailable]");
    await expect(page.getByRole("heading", { name: "Ask anything" })).toBeVisible();
  });

  test("adds a line with Shift+Enter instead of sending", async ({ page }) => {
    const input = page.getByRole("textbox", { name: "Your question" });
    await input.fill("line one");
    await input.press("Shift+Enter");
    await input.pressSequentially("line two");
    await expect(input).toHaveValue("line one\nline two");

    await input.press("Enter");

    await expect(conversation(page).getByText(/Echo: line one\s+line two/)).toBeVisible();
  });

  test("starts a new conversation that forgets the previous turns", async ({ page }) => {
    await ask(page, "My name is Ana.");
    await expect(conversation(page).getByText("Echo: My name is Ana.")).toBeVisible();

    await page.getByRole("button", { name: "New conversation" }).click();
    await expect(page.getByRole("heading", { name: "Ask anything" })).toBeVisible();
    await ask(page, "What is my name?");

    await expect(conversation(page).getByText("I do not know your name yet.")).toBeVisible();
  });

  test("sends an attached image to the model and shows it in the conversation", async ({ page }) => {
    // A 1×1 PNG.
    const png = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==", "base64");

    await page.getByTestId("image-input").setInputFiles({ name: "pixel.png", mimeType: "image/png", buffer: png });
    await expect(page.getByRole("list", { name: "Images for your next question" }).getByRole("img", { name: "Attached image pixel.png" })).toBeVisible();
    await expectNoA11yViolations(page);

    await ask(page, "What is in this image?");

    await expect(conversation(page).getByText("I see 1 image (image/png): What is in this image?")).toBeVisible();
    await expect(conversation(page).getByRole("img", { name: "Attached image pixel.png" })).toBeVisible();
    await expect(page.getByRole("list", { name: "Images for your next question" })).toBeHidden();
  });

  test("rejects a file that is not an image before sending it", async ({ page }) => {
    await page.getByTestId("image-input").setInputFiles({ name: "notes.png", mimeType: "image/png", buffer: Buffer.from("not an image") });

    await expect(page.getByText("notes.png is not a PNG, JPEG, WebP or GIF image.")).toBeVisible();
  });

  test("has no serious accessibility violations with a conversation", async ({ page }) => {
    await expectNoA11yViolations(page);

    await ask(page, "Hello");
    await expect(conversation(page).getByText("Echo: Hello")).toBeVisible();

    await expectNoA11yViolations(page);
  });
});
