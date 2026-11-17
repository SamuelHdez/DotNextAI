import { expect, test, type Page } from "@playwright/test";
import { expectNoA11yViolations } from "./a11y";

// The suite shares one API and one database per run (playwright.config.ts): names are unique per test.
const unique = () => `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`.replace(/[^a-z]/g, "x");

async function ask(page: Page, question: string) {
  const input = page.getByRole("textbox", { name: "Your question" });
  await input.fill(question);
  await input.press("Enter");
}

const conversation = (page: Page) => page.getByRole("log", { name: "Conversation" });
// The Saved chats section of the side navigation, under Workspace.
const panel = (page: Page) => page.getByRole("navigation", { name: "Main navigation" }).getByRole("region", { name: "Saved chats" });

async function chatOption(page: Page, title: string, item: string) {
  await panel(page).getByRole("button", { name: `Options for ${title}` }).click();
  await page.getByRole("menuitem", { name: item }).click();
}

test.describe("saved chats", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/chat");
    await expect(page.getByRole("heading", { name: "Ask anything" })).toBeVisible();
  });

  test("keeps a chat after a reload and answers with its saved history", async ({ page }) => {
    const name = `Zoe${unique()}`;
    await ask(page, `My name is ${name}.`);
    await expect(conversation(page).getByText(`Echo: My name is ${name}.`)).toBeVisible();

    const link = panel(page).getByRole("link", { name: `My name is ${name}.` });
    await expect(link).toHaveAttribute("aria-current", "page");
    await expect(page).toHaveURL(/\/chat\?c=[0-9a-f-]{36}$/);

    await page.reload();
    await expect(conversation(page).getByText(`Echo: My name is ${name}.`)).toBeVisible();
    await ask(page, "What is my name?");

    // The browser sent only the question: the API read the history from the database.
    await expect(conversation(page).getByText(`Your name is ${name}.`)).toBeVisible();
  });

  test("opens a saved chat from the list and starts new ones apart", async ({ page }) => {
    const topic = unique();
    await ask(page, `First ${topic}`);
    await expect(conversation(page).getByText(`Echo: First ${topic}`)).toBeVisible();

    await page.getByRole("button", { name: "New conversation", exact: true }).click();
    await expect(page).toHaveURL(/\/chat$/);
    await ask(page, `Second ${topic}`);
    await expect(conversation(page).getByText(`Echo: Second ${topic}`)).toBeVisible();

    await panel(page).getByRole("link", { name: `First ${topic}` }).click();
    await expect(conversation(page).getByText(`Echo: First ${topic}`)).toBeVisible();
    await expect(conversation(page).getByText(`Echo: Second ${topic}`)).toBeHidden();
    await expectNoA11yViolations(page);
  });

  test("opens a saved chat from another page", async ({ page }) => {
    const topic = unique();
    await ask(page, `Elsewhere ${topic}`);
    await expect(conversation(page).getByText(`Echo: Elsewhere ${topic}`)).toBeVisible();

    await page.getByRole("link", { name: "Images" }).click();
    await expect(page.getByRole("heading", { name: "Images", level: 1 })).toBeVisible();
    await panel(page).getByRole("link", { name: `Elsewhere ${topic}` }).click();

    await expect(page).toHaveURL(/\/chat\?c=[0-9a-f-]{36}$/);
    await expect(conversation(page).getByText(`Echo: Elsewhere ${topic}`)).toBeVisible();
    await expect(panel(page).getByRole("link", { name: `Elsewhere ${topic}` })).toHaveAttribute("aria-current", "page");
  });

  test("organises chats in folders: create, move, rename and delete", async ({ page }) => {
    const topic = unique();
    const folder = `Folder ${topic}`;
    await ask(page, `Plan ${topic}`);
    await expect(conversation(page).getByText(`Echo: Plan ${topic}`)).toBeVisible();

    await panel(page).getByRole("button", { name: "New folder" }).click();
    const create = page.getByRole("dialog", { name: "New folder" });
    await create.getByRole("textbox", { name: "Folder name" }).fill(folder);
    await expectNoA11yViolations(page);
    await create.getByRole("button", { name: "Create" }).click();
    await expect(panel(page).getByRole("region", { name: folder })).toBeVisible();

    await chatOption(page, `Plan ${topic}`, "Move to folder…");
    const move = page.getByRole("dialog", { name: "Move chat" });
    await move.getByRole("combobox", { name: `Folder for “Plan ${topic}”` }).selectOption({ label: folder });
    await move.getByRole("button", { name: "Move" }).click();
    await expect(panel(page).getByRole("region", { name: folder }).getByRole("link", { name: `Plan ${topic}` })).toBeVisible();

    await chatOption(page, `Plan ${topic}`, "Rename…");
    const rename = page.getByRole("dialog", { name: "Rename chat" });
    await rename.getByRole("textbox", { name: "Title" }).fill(`Offsite ${topic}`);
    await rename.getByRole("button", { name: "Save" }).click();
    await expect(panel(page).getByRole("link", { name: `Offsite ${topic}` })).toBeVisible();

    // Kept after a reload, in its folder.
    await page.reload();
    await expect(panel(page).getByRole("region", { name: folder }).getByRole("link", { name: `Offsite ${topic}` })).toBeVisible();
    await expectNoA11yViolations(page);

    await panel(page).getByRole("button", { name: `Options for the folder ${folder}` }).click();
    await page.getByRole("menuitem", { name: "Delete…" }).click();
    await page.getByRole("alertdialog", { name: "Delete this folder?" }).getByRole("button", { name: "Delete" }).click();
    await expect(panel(page).getByRole("region", { name: folder })).toBeHidden();
    await expect(panel(page).getByRole("link", { name: `Offsite ${topic}` })).toBeVisible();

    await chatOption(page, `Offsite ${topic}`, "Delete…");
    await page.getByRole("alertdialog", { name: "Delete this chat?" }).getByRole("button", { name: "Delete" }).click();
    await expect(panel(page).getByRole("link", { name: `Offsite ${topic}` })).toBeHidden();
    await expect(page.getByRole("heading", { name: "Ask anything" })).toBeVisible();
  });

  test("gives a folder an icon and a colour, and collapses it", async ({ page }) => {
    const topic = unique();
    const folder = `Look ${topic}`;
    await ask(page, `Inside ${topic}`);
    await expect(conversation(page).getByText(`Echo: Inside ${topic}`)).toBeVisible();
    await panel(page).getByRole("button", { name: "New folder" }).click();
    await page.getByRole("dialog", { name: "New folder" }).getByRole("textbox", { name: "Folder name" }).fill(folder);
    await page.getByRole("dialog", { name: "New folder" }).getByRole("button", { name: "Create" }).click();
    await chatOption(page, `Inside ${topic}`, "Move to folder…");
    await page.getByRole("dialog", { name: "Move chat" }).getByRole("combobox").selectOption({ label: folder });
    await page.getByRole("dialog", { name: "Move chat" }).getByRole("button", { name: "Move" }).click();
    await expect(panel(page).getByRole("region", { name: folder }).getByRole("link", { name: `Inside ${topic}` })).toBeVisible();

    await panel(page).getByRole("button", { name: `Options for the folder ${folder}` }).click();
    await page.getByRole("menuitem", { name: "Icon and colour…" }).click();
    const dialog = page.getByRole("dialog", { name: "Folder appearance" });
    await dialog.getByRole("radiogroup", { name: "Icon" }).getByRole("radio", { name: "Rocket" }).click();
    await dialog.getByRole("radiogroup", { name: "Colour" }).getByRole("radio", { name: "Purple" }).click();
    await expectNoA11yViolations(page);
    await dialog.getByRole("button", { name: "Save" }).click();
    await expect(dialog).toBeHidden();

    const toggle = panel(page).getByRole("button", { name: folder, exact: true });
    await toggle.click();
    await expect(toggle).toHaveAttribute("aria-expanded", "false");
    await expect(panel(page).getByRole("link", { name: `Inside ${topic}` })).toBeHidden();

    // The appearance is saved on the server, the collapsed state in this browser: both survive a reload.
    await page.reload();
    await expect(panel(page).getByRole("button", { name: folder, exact: true })).toHaveAttribute("aria-expanded", "false");
    await panel(page).getByRole("button", { name: `Options for the folder ${folder}` }).click();
    await page.getByRole("menuitem", { name: "Icon and colour…" }).click();
    await expect(page.getByRole("dialog", { name: "Folder appearance" }).getByRole("radio", { name: "Rocket" })).toBeChecked();
    await expect(page.getByRole("dialog", { name: "Folder appearance" }).getByRole("radio", { name: "Purple" })).toBeChecked();
  });

  test("refuses a folder name that is taken", async ({ page }) => {
    const folder = `Taken ${unique()}`;
    for (const attempt of [folder, folder.toUpperCase()]) {
      await panel(page).getByRole("button", { name: "New folder" }).click();
      const dialog = page.getByRole("dialog", { name: "New folder" });
      await dialog.getByRole("textbox", { name: "Folder name" }).fill(attempt);
      await dialog.getByRole("button", { name: "Create" }).click();
    }

    await expect(page.getByRole("dialog", { name: "New folder" }).getByText("A folder with that name already exists.")).toBeVisible();
  });

  test("keeps a stopped answer, marked as stopped", async ({ page }) => {
    const topic = unique();
    await ask(page, `Long story ${topic} [e2e:slow]`);
    await expect(conversation(page).locator("[data-streaming]")).toHaveText(/^Echo:/);
    await page.getByRole("button", { name: "Stop" }).click();
    await expect(conversation(page).getByText("Stopped before the end.")).toBeVisible();

    await page.reload();

    await expect(conversation(page).getByText("Stopped before the end.")).toBeVisible();
    await expect(conversation(page).getByText(`Long story ${topic} [e2e:slow]`)).toBeVisible();
  });

  test("keeps the images of a question and loads them from the API", async ({ page }) => {
    const png = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==", "base64");
    await page.getByTestId("image-input").setInputFiles({ name: "pixel.png", mimeType: "image/png", buffer: png });
    await ask(page, `What is this ${unique()}?`);
    await expect(conversation(page).getByText(/^I see 1 image \(image\/png\)/)).toBeVisible();

    await page.reload();

    // Served by GET /api/conversations/{id}/images/{imageId}: the CSP allows the API's images, and the file loads.
    const image = conversation(page).getByRole("img", { name: "Attached image 1" });
    await expect(image).toBeVisible();
    await expect(image).toHaveAttribute("src", /\/api\/conversations\/[0-9a-f-]{36}\/images\/[0-9a-f-]{36}$/);
    await expect.poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth)).toBe(1);
  });

  test("starts a new conversation when the chat of the address no longer exists", async ({ page }) => {
    await page.goto("/chat?c=00000000-0000-0000-0000-000000000000");

    await expect(page.getByText("That chat no longer exists. You are in a new conversation.")).toBeVisible();
    await expect(page.getByRole("heading", { name: "Ask anything" })).toBeVisible();
    await expect(page).toHaveURL(/\/chat$/);
  });
});
