import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ChatWorkspace } from "@/components/savedChats/ChatWorkspace";
import { SavedChatsProvider } from "@/components/savedChats/SavedChatsProvider";
import { SideNav } from "@/components/shell/SideNav";
import { chatPageFetch, conversationSummary, folderInfo, jsonResponse, renderWithProviders, savedChatsBackend, sseResponse } from "@/test/render";

const navigation = vi.hoisted(() => ({ pathname: "/", push: vi.fn() }));
vi.mock("next/navigation", () => ({ usePathname: () => navigation.pathname, useRouter: () => ({ push: navigation.push }) }));

type Fetch = ReturnType<typeof vi.fn>;
type Backend = ReturnType<typeof savedChatsBackend>;

let fetchMock: Fetch;

function start(backend: Backend, initialConversationId: string | null = null, locale: "en" | "es" = "en") {
  vi.stubGlobal("fetch", chatPageFetch(fetchMock, undefined, backend));
  return renderWithProviders(<Shell initialConversationId={initialConversationId} />, { locale });
}

/** The side navigation (with the saved chats) next to the chat page, as the app shell renders them. */
function Shell({ initialConversationId = null, page = true }: { initialConversationId?: string | null; page?: boolean }) {
  return (
    <SavedChatsProvider>
      <SideNav type="inline" open />
      {page && <ChatWorkspace initialConversationId={initialConversationId} />}
    </SavedChatsProvider>
  );
}

/** A complete answer whose `done` carries the conversation as the server saved it. */
function savedAnswer(text: string, conversation: ReturnType<typeof conversationSummary>) {
  return sseResponse({ event: "delta", data: { text } }, { event: "done", data: { answer: text, model: "gpt-5.6-luna", truncated: false, usage: null, conversation } });
}

/** The Saved chats section of the side navigation. */
const panel = () => screen.getByRole("region", { name: "Saved chats" });
/** The section once a dialog has finished closing (Fluent hides the rest of the page until then). */
const panelAfterDialog = () => screen.findByRole("region", { name: "Saved chats" });

async function ask(user: ReturnType<typeof userEvent.setup>, text: string) {
  await user.type(screen.getByRole("textbox", { name: "Your question" }), text);
  await user.click(screen.getByRole("button", { name: "Send" }));
}

async function chooseOption(user: ReturnType<typeof userEvent.setup>, menuName: string, item: string) {
  await user.click(within(panel()).getByRole("button", { name: menuName }));
  await user.click(await screen.findByRole("menuitem", { name: item }));
}

beforeEach(() => {
  fetchMock = vi.fn();
  navigation.pathname = "/";
  navigation.push.mockReset();
  window.localStorage.clear();
  window.history.replaceState(null, "", "/");
});

afterEach(() => vi.unstubAllGlobals());

describe("ChatWorkspace", () => {
  it("lists the saved chats under the workspace links of the side navigation", async () => {
    start(savedChatsBackend({ conversations: [conversationSummary("c1", { title: "Plan" })] }));

    const nav = screen.getByRole("navigation", { name: "Main navigation" });
    const section = await within(nav).findByRole("region", { name: "Saved chats" });
    expect(within(section).getByRole("link", { name: "Plan" })).toBeInTheDocument();
    expect(within(nav).getByRole("group", { name: "Workspace" })).toBeInTheDocument();
  });

  it("opens a saved chat from another page by navigating to the chat page", async () => {
    navigation.pathname = "/images";
    vi.stubGlobal("fetch", chatPageFetch(fetchMock, undefined, savedChatsBackend({ conversations: [conversationSummary("c1", { title: "Plan" })] })));
    const user = userEvent.setup();
    renderWithProviders(<Shell page={false} />);

    await user.click(await within(panel()).findByRole("link", { name: "Plan" }));

    expect(navigation.push).toHaveBeenCalledWith("/?c=c1");
  });

  it("lists the saved chats by folder, the untitled one in the selected language", async () => {
    start(
      savedChatsBackend({
        folders: [folderInfo("f1", "Work")],
        conversations: [conversationSummary("c1", { title: "Offsite plan", folderId: "f1" }), conversationSummary("c2")],
      }),
      null,
      "es",
    );

    const list = await screen.findByRole("region", { name: "Chats guardados" });
    const work = within(list).getByRole("region", { name: "Work" });
    expect(within(work).getByRole("link", { name: "Offsite plan" })).toHaveAttribute("href", "/?c=c1");
    const unfiled = within(list).getByRole("region", { name: "Sin carpeta" });
    expect(within(unfiled).getByRole("link", { name: "Nueva conversación" })).toBeInTheDocument();
  });

  it("says when there are no saved chats yet", async () => {
    start(savedChatsBackend());

    expect(await within(panel()).findByText("No saved chats yet. A chat is saved when you ask its first question.")).toBeInTheDocument();
  });

  it("saves a new chat with its first question and shows it as the current one", async () => {
    const backend = savedChatsBackend();
    fetchMock.mockImplementation(() => Promise.resolve(savedAnswer("Hi Ana!", conversationSummary("c1", { title: "My name is Ana.", questionCount: 1, updatedAt: "2026-11-16T11:00:00Z" }))));
    const user = userEvent.setup();
    start(backend);
    await within(panel()).findByText(/No saved chats yet/);

    await ask(user, "My name is Ana.");

    const link = await within(panel()).findByRole("link", { name: "My name is Ana." });
    expect(link).toHaveAttribute("aria-current", "page");
    expect(window.location.search).toBe("?c=c1");
    expect(String(fetchMock.mock.calls[0][0])).toBe("http://api.test/api/conversations/c1/messages/stream");
  });

  it("keeps no empty chat when its first question fails", async () => {
    const backend = savedChatsBackend();
    fetchMock.mockResolvedValue(jsonResponse({ status: 503, failure: "ModelUnavailable" }, 503, "application/problem+json"));
    const user = userEvent.setup();
    start(backend);
    await within(panel()).findByText(/No saved chats yet/);

    await ask(user, "Hi");

    expect(await screen.findByText("The model is temporarily unavailable. Try again in a few minutes.")).toBeInTheDocument();
    await waitFor(() => expect(backend.state.conversations).toEqual([]));
    expect(within(panel()).queryByRole("link")).not.toBeInTheDocument();
    expect(window.location.search).toBe("");
    expect(screen.getByRole("textbox", { name: "Your question" })).toHaveValue("Hi");
  });

  it("keeps a new chat whose connection drops after the first words (the server saves them as stopped)", async () => {
    const backend = savedChatsBackend();
    fetchMock.mockResolvedValue(sseResponse({ event: "delta", data: { text: "Hello " } }));
    const user = userEvent.setup();
    start(backend);
    await within(panel()).findByText(/No saved chats yet/);

    await ask(user, "Hi");

    expect(await screen.findByText("Cannot reach the DotNextAI API. Check that the backend is running.")).toBeInTheDocument();
    expect(backend.state.conversations.map((c) => c.id)).toEqual(["c1"]);
  });

  it("deletes a new chat whose answer breaks with an error event (the server saved nothing)", async () => {
    const backend = savedChatsBackend();
    fetchMock.mockResolvedValue(
      sseResponse({ event: "delta", data: { text: "Hello " } }, { event: "error", data: { status: 502, failure: "Unavailable", title: "x", detail: "x" } }),
    );
    const user = userEvent.setup();
    start(backend);
    await within(panel()).findByText(/No saved chats yet/);

    await ask(user, "Hi");

    expect(await screen.findByText("The model provider is unavailable right now. Try again later.")).toBeInTheDocument();
    await waitFor(() => expect(backend.state.conversations).toEqual([]));
  });

  it("opens a saved chat with its messages and images, and goes on in it", async () => {
    const backend = savedChatsBackend({
      conversations: [conversationSummary("c1", { title: "Pixel", questionCount: 1 }), conversationSummary("c2", { title: "Other" })],
      details: {
        c1: {
          conversation: conversationSummary("c1", { title: "Pixel", questionCount: 1 }),
          messages: [
            { id: "q", role: "user", text: "What is this?", images: [{ id: "i1", mediaType: "image/png", size: 70, url: "/api/conversations/c1/images/i1" }], model: null, usage: null, truncated: false, stopped: false, createdAt: "x" },
            { id: "a", role: "assistant", text: "A pixel.", images: [], model: "gpt-5.6-luna", usage: null, truncated: false, stopped: true, createdAt: "x" },
          ],
        },
      },
    });
    fetchMock.mockImplementation(() => Promise.resolve(savedAnswer("Black.", conversationSummary("c1", { title: "Pixel", questionCount: 2 }))));
    const user = userEvent.setup();
    start(backend);

    await user.click(await within(panel()).findByRole("link", { name: "Pixel" }));

    const log = await screen.findByRole("log", { name: "Conversation" });
    expect(await within(log).findByText("A pixel.")).toBeInTheDocument();
    expect(within(log).getByText("Stopped before the end.")).toBeInTheDocument();
    expect(within(log).getByRole("img", { name: "Attached image 1" })).toHaveAttribute("src", "http://api.test/api/conversations/c1/images/i1");
    expect(window.location.search).toBe("?c=c1");

    await ask(user, "What colour?");
    await within(log).findByText("Black.");
    expect(String(fetchMock.mock.calls[0][0])).toBe("http://api.test/api/conversations/c1/messages/stream");
    expect(JSON.parse((fetchMock.mock.calls[0][1] as RequestInit).body as string)).toEqual({ question: "What colour?" });
  });

  it("opens the chat of the address and starts a new one when it no longer exists", async () => {
    start(savedChatsBackend(), "gone");

    expect(await screen.findByText("That chat no longer exists. You are in a new conversation.")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Ask anything" })).toBeInTheDocument();
    expect(window.location.search).toBe("");
  });

  it("creates a folder and refuses a duplicate name under the field", async () => {
    const user = userEvent.setup();
    start(savedChatsBackend({ folders: [folderInfo("f0", "Work")] }));
    await within(panel()).findByRole("region", { name: "Work" });

    await user.click(within(panel()).getByRole("button", { name: "New folder" }));
    const dialog = await screen.findByRole("dialog", { name: "New folder" });
    await user.type(within(dialog).getByRole("textbox", { name: "Folder name" }), "work");
    await user.click(within(dialog).getByRole("button", { name: "Create" }));
    expect(await within(dialog).findByText("A folder with that name already exists.")).toBeInTheDocument();

    await user.clear(within(dialog).getByRole("textbox", { name: "Folder name" }));
    await user.type(within(dialog).getByRole("textbox", { name: "Folder name" }), "  Archive ");
    await user.click(within(dialog).getByRole("button", { name: "Create" }));

    expect(within(await panelAfterDialog()).getAllByRole("heading", { level: 4 }).map((h) => h.textContent)).toEqual(["Archive", "Work"]);
  });

  it("checks the name before calling the API", async () => {
    const user = userEvent.setup();
    start(savedChatsBackend());
    await within(panel()).findByText(/No saved chats yet/);

    await user.click(within(panel()).getByRole("button", { name: "New folder" }));
    const dialog = await screen.findByRole("dialog", { name: "New folder" });
    await user.click(within(dialog).getByRole("button", { name: "Create" }));

    expect(await within(dialog).findByText("Type a name first.")).toBeInTheDocument();
  });

  it("moves a chat into a folder and renames it", async () => {
    const backend = savedChatsBackend({
      folders: [folderInfo("f1", "Work")],
      conversations: [conversationSummary("c1", { title: "Plan" })],
    });
    const user = userEvent.setup();
    start(backend);
    await within(panel()).findByRole("link", { name: "Plan" });

    await chooseOption(user, "Options for Plan", "Move to folder…");
    const move = await screen.findByRole("dialog", { name: "Move chat" });
    await user.selectOptions(within(move).getByRole("combobox", { name: "Folder for “Plan”" }), "Work");
    await user.click(within(move).getByRole("button", { name: "Move" }));
    await waitFor(async () => expect(within(within(await panelAfterDialog()).getByRole("region", { name: "Work" })).getByRole("link", { name: "Plan" })).toBeInTheDocument());
    expect(backend.state.conversations[0].folderId).toBe("f1");

    await chooseOption(user, "Options for Plan", "Rename…");
    const rename = await screen.findByRole("dialog", { name: "Rename chat" });
    const title = within(rename).getByRole("textbox", { name: "Title" });
    expect(title).toHaveValue("Plan");
    await user.clear(title);
    await user.type(title, "Offsite");
    await user.click(within(rename).getByRole("button", { name: "Save" }));

    expect(await within(await panelAfterDialog()).findByRole("link", { name: "Offsite" })).toBeInTheDocument();
  });

  it("deletes the open chat after asking, and starts a new one", async () => {
    const backend = savedChatsBackend({
      conversations: [conversationSummary("c1", { title: "Plan", questionCount: 1 })],
      details: {
        c1: {
          conversation: conversationSummary("c1", { title: "Plan", questionCount: 1 }),
          messages: [
            { id: "q", role: "user", text: "Hi", images: [], model: null, usage: null, truncated: false, stopped: false, createdAt: "x" },
            { id: "a", role: "assistant", text: "Hello!", images: [], model: "gpt-5.6-luna", usage: null, truncated: false, stopped: false, createdAt: "x" },
          ],
        },
      },
    });
    const user = userEvent.setup();
    start(backend, "c1");
    await screen.findByText("Hello!");

    await chooseOption(user, "Options for Plan", "Delete…");
    const confirm = await screen.findByRole("alertdialog", { name: "Delete this chat?" });
    expect(confirm).toHaveTextContent("“Plan” and its messages and images will be deleted.");
    await user.click(within(confirm).getByRole("button", { name: "Delete" }));

    expect(await screen.findByRole("heading", { name: "Ask anything" })).toBeInTheDocument();
    expect(within(panel()).queryByRole("link", { name: "Plan" })).not.toBeInTheDocument();
    expect(backend.state.conversations).toEqual([]);
    expect(window.location.search).toBe("");
  });

  it("collapses a folder's chats and remembers it in this browser", async () => {
    const backend = savedChatsBackend({
      folders: [folderInfo("f1", "Work")],
      conversations: [conversationSummary("c1", { title: "Plan", folderId: "f1" })],
    });
    const user = userEvent.setup();
    const { unmount } = start(backend);
    const toggle = await within(panel()).findByRole("button", { name: "Work" });
    expect(toggle).toHaveAttribute("aria-expanded", "true");

    await user.click(toggle);

    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(within(panel()).queryByRole("link", { name: "Plan" })).not.toBeInTheDocument();
    unmount();
    start(backend);
    expect(await within(panel()).findByRole("button", { name: "Work" })).toHaveAttribute("aria-expanded", "false");

    await user.click(within(panel()).getByRole("button", { name: "Work" }));
    expect(within(panel()).getByRole("link", { name: "Plan" })).toBeInTheDocument();
  });

  it("changes a folder's icon and colour", async () => {
    const backend = savedChatsBackend({ folders: [folderInfo("f1", "Work")] });
    const user = userEvent.setup();
    start(backend, null, "es");
    const section = await screen.findByRole("region", { name: "Chats guardados" });

    await user.click(within(section).getByRole("button", { name: "Opciones de la carpeta Work" }));
    await user.click(await screen.findByRole("menuitem", { name: "Icono y color…" }));
    const dialog = await screen.findByRole("dialog", { name: "Aspecto de la carpeta" });
    expect(within(dialog).getByRole("radio", { name: "Carpeta" })).toBeChecked();
    await user.click(within(within(dialog).getByRole("radiogroup", { name: "Icono" })).getByRole("radio", { name: "Cohete" }));
    await user.click(within(within(dialog).getByRole("radiogroup", { name: "Color" })).getByRole("radio", { name: "Azul" }));
    await user.click(within(dialog).getByRole("button", { name: "Guardar" }));

    await waitFor(() => expect(backend.state.folders[0]).toMatchObject({ icon: "rocket", color: "blue" }));
  });

  it("deletes a folder and keeps its chats without a folder", async () => {
    const user = userEvent.setup();
    start(
      savedChatsBackend({
        folders: [folderInfo("f1", "Work")],
        conversations: [conversationSummary("c1", { title: "Plan", folderId: "f1" })],
      }),
    );
    await within(panel()).findByRole("region", { name: "Work" });

    await chooseOption(user, "Options for the folder Work", "Delete…");
    await user.click(within(await screen.findByRole("alertdialog", { name: "Delete this folder?" })).getByRole("button", { name: "Delete" }));

    await waitFor(() => expect(within(panel()).queryByRole("region", { name: "Work" })).not.toBeInTheDocument());
    expect(within(panel()).getByRole("link", { name: "Plan" })).toBeInTheDocument();
  });

  it("offers to retry when the list cannot be loaded", async () => {
    let fail = true;
    const backend = savedChatsBackend({ conversations: [conversationSummary("c1", { title: "Plan" })] });
    vi.stubGlobal("fetch", (input: RequestInfo | URL, init?: RequestInit) =>
      fail && new URL(String(input)).pathname === "/api/conversations"
        ? Promise.resolve(jsonResponse({ status: 500 }, 500, "application/problem+json"))
        : chatPageFetch(fetchMock, undefined, backend)(input, init),
    );
    const user = userEvent.setup();
    renderWithProviders(<Shell />);

    const retry = await within(panel()).findByRole("button", { name: "Try again" });
    fail = false;
    await user.click(retry);

    expect(await within(panel()).findByRole("link", { name: "Plan" })).toBeInTheDocument();
  });
});
