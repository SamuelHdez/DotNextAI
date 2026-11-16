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

  test("streams the answer: thinking first, then the text as it is written", async ({ page }) => {
    await ask(page, "Take your time [e2e:slow]");

    await expect(page.getByText("DotNextAI is thinking…")).toBeVisible();
    await expect(page.getByRole("button", { name: "Stop" })).toBeVisible();

    // A partial answer: the first words, before the rest arrives.
    const partial = conversation(page).locator("[data-streaming]");
    await expect(partial).toHaveText(/^Echo: Take/);
    await expect(partial).not.toHaveText("Echo: Take your time [e2e:slow]");

    await expect(conversation(page).getByText("Echo: Take your time [e2e:slow]")).toBeVisible();
    await expect(conversation(page).getByText("e2e-model")).toBeVisible();
    await expect(page.getByRole("button", { name: "Send" })).toBeVisible();
  });

  test("stops the answer and keeps what had arrived", async ({ page }) => {
    await ask(page, "Tell me a long story [e2e:slow]");
    await expect(conversation(page).locator("[data-streaming]")).toHaveText(/^Echo:/);

    await page.getByRole("button", { name: "Stop" }).click();

    await expect(conversation(page).getByText("Stopped before the end.")).toBeVisible();
    await expect(conversation(page).getByText(/^Echo:/)).toBeVisible();
    await expect(page.getByRole("button", { name: "Send" })).toBeVisible();
  });

  test("reports an answer that breaks mid-stream and gives the question back", async ({ page }) => {
    await ask(page, "Are you still there? [e2e:drop]");

    await expect(page.getByText("The model provider is unavailable right now. Try again later.")).toBeVisible();
    await expect(page.getByRole("textbox", { name: "Your question" })).toHaveValue("Are you still there? [e2e:drop]");
    await expect(page.getByRole("heading", { name: "Ask anything" })).toBeVisible();
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

  test("answers with the chosen model and remembers the choice", async ({ page }) => {
    const picker = page.getByRole("combobox", { name: "Model" });
    await expect(picker).toHaveText("e2e-model (default)");

    await picker.click();
    // Only chat models are offered (the fake proxy also lists an embedding and an image model).
    await expect(page.getByRole("option")).toHaveText(["e2e-model (default)", "e2e-model-fast"]);
    await page.getByRole("option", { name: "e2e-model-fast" }).click();
    await ask(page, "Which model are you?");

    await expect(conversation(page).getByText("Echo: Which model are you?")).toBeVisible();
    await expect(conversation(page).getByText("e2e-model-fast", { exact: true })).toBeVisible();

    await page.reload();
    await expect(page.getByRole("combobox", { name: "Model" })).toHaveText("e2e-model-fast");
  });

  test("shows the tokens of each answer and the growing total of the conversation", async ({ page }) => {
    await ask(page, "Hello");
    await expect(conversation(page).getByText(/^\d[\d,]* in · \d[\d,]* out tokens$/)).toHaveCount(1);
    const total = page.getByText(/^This conversation: [\d,]+ tokens/);
    await expect(total).toContainText("over 1 answers");
    const first = Number((await total.textContent())!.match(/([\d,]+) tokens/)![1].replaceAll(",", ""));

    await ask(page, "Hello again");

    await expect(total).toContainText("over 2 answers");
    const second = Number((await total.textContent())!.match(/([\d,]+) tokens/)![1].replaceAll(",", ""));
    // The second question resends the first turn, so it costs more than the first one did.
    expect(second).toBeGreaterThan(first * 2);
    await expect(page.getByText("Every question resends the conversation, so input tokens grow with each turn.")).toBeVisible();
  });

  test("has no serious accessibility violations with a conversation", async ({ page }) => {
    await expectNoA11yViolations(page);

    await ask(page, "Hello");
    await expect(conversation(page).getByText("Echo: Hello")).toBeVisible();

    await expectNoA11yViolations(page);
  });
});
