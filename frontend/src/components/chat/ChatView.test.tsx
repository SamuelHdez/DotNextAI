import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ChatView } from "@/components/chat/ChatView";
import { jsonResponse, renderWithProviders } from "@/test/render";

type Fetch = ReturnType<typeof vi.fn>;

function answer(text: string, truncated = false) {
  return jsonResponse({ answer: text, model: "gpt-5.6-luna", truncated });
}

function sentBodies(fetchMock: Fetch) {
  return fetchMock.mock.calls.map(([, init]) => JSON.parse((init as RequestInit).body as string));
}

let fetchMock: Fetch;

beforeEach(() => {
  fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => vi.unstubAllGlobals());

async function ask(user: ReturnType<typeof userEvent.setup>, text: string) {
  await user.type(screen.getByRole("textbox", { name: "Your question" }), text);
  await user.click(screen.getByRole("button", { name: "Send" }));
}

describe("ChatView", () => {
  it("shows suggestions while the conversation is empty", () => {
    renderWithProviders(<ChatView />);

    expect(screen.getByRole("heading", { name: "Ask anything" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Write a haiku about .NET." })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New conversation" })).toBeDisabled();
  });

  it("sends a question and shows the question and the answer", async () => {
    fetchMock.mockResolvedValue(answer("Hello Ana!"));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "  My name is Ana.  ");

    const log = await screen.findByRole("log", { name: "Conversation" });
    expect(await within(log).findByText("Hello Ana!")).toBeInTheDocument();
    expect(within(log).getByText("My name is Ana.")).toBeInTheDocument();
    expect(within(log).getByText("gpt-5.6-luna")).toBeInTheDocument();
    expect(sentBodies(fetchMock)).toEqual([{ question: "My name is Ana.", history: [] }]);
    expect(screen.getByRole("textbox", { name: "Your question" })).toHaveValue("");
  });

  it("sends the previous turns with the next question", async () => {
    fetchMock.mockResolvedValueOnce(answer("Hello Ana!")).mockResolvedValueOnce(answer("Your name is Ana."));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "My name is Ana.");
    await screen.findByText("Hello Ana!");
    await ask(user, "What is my name?");
    await screen.findByText("Your name is Ana.");

    expect(sentBodies(fetchMock)[1]).toEqual({
      question: "What is my name?",
      history: [
        { role: "user", text: "My name is Ana." },
        { role: "assistant", text: "Hello Ana!" },
      ],
    });
  });

  it("sends with Enter and adds a line with Shift+Enter", async () => {
    fetchMock.mockResolvedValue(answer("ok"));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);
    const input = screen.getByRole("textbox", { name: "Your question" });

    await user.type(input, "line one{Shift>}{Enter}{/Shift}line two");
    expect(fetchMock).not.toHaveBeenCalled();
    await user.keyboard("{Enter}");

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    expect(sentBodies(fetchMock)[0].question).toBe("line one\nline two");
  });

  it("asks a suggestion when it is clicked", async () => {
    fetchMock.mockResolvedValue(answer("Code flows like water"));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await user.click(screen.getByRole("button", { name: "Write a haiku about .NET." }));

    expect(await screen.findByText("Code flows like water")).toBeInTheDocument();
    expect(sentBodies(fetchMock)[0].question).toBe("Write a haiku about .NET.");
  });

  it("shows the failure in the selected language and gives the question back", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ status: 503, failure: "ModelUnavailable", detail: "English detail" }, 503, "application/problem+json"));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />, { locale: "es" });

    await user.type(screen.getByRole("textbox", { name: "Tu pregunta" }), "Hola");
    await user.click(screen.getByRole("button", { name: "Enviar" }));

    expect(await screen.findByText("El modelo no está disponible en este momento. Vuelve a intentarlo en unos minutos.")).toBeInTheDocument();
    expect(screen.queryByText("English detail")).not.toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Tu pregunta" })).toHaveValue("Hola");
    expect(screen.queryByRole("log")).not.toBeInTheDocument();
  });

  it("tells when the API cannot be reached", async () => {
    fetchMock.mockRejectedValue(new TypeError("Failed to fetch"));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "Hi");

    expect(await screen.findByText("Cannot reach the DotNextAI API. Check that the backend is running.")).toBeInTheDocument();
  });

  it("marks a truncated answer", async () => {
    fetchMock.mockResolvedValue(answer("partial", true));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "Tell me everything");

    expect(await screen.findByText("The answer was cut off because it reached the maximum length.")).toBeInTheDocument();
  });

  it("starts a new conversation without the previous turns", async () => {
    fetchMock.mockResolvedValueOnce(answer("first")).mockResolvedValueOnce(answer("second"));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "one");
    await screen.findByText("first");
    await user.click(screen.getByRole("button", { name: "New conversation" }));
    expect(screen.getByRole("heading", { name: "Ask anything" })).toBeInTheDocument();
    await ask(user, "two");
    await screen.findByText("second");

    expect(sentBodies(fetchMock)[1]).toEqual({ question: "two", history: [] });
  });

  it("does not send an empty question", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await user.type(screen.getByRole("textbox", { name: "Your question" }), "   {Enter}");

    expect(fetchMock).not.toHaveBeenCalled();
    expect(await screen.findByText("Type a question first.")).toBeInTheDocument();
  });
});
