import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ChatView } from "@/components/chat/ChatView";
import { controlledStream, jsonResponse, renderWithProviders, streamedAnswer } from "@/test/render";

type Fetch = ReturnType<typeof vi.fn>;

function answer(text: string, truncated = false) {
  return streamedAnswer(text, truncated);
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

  it("calls the streaming endpoint", async () => {
    fetchMock.mockResolvedValue(answer("ok"));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "Hi");
    await screen.findByText("ok");

    expect(String(fetchMock.mock.calls[0][0])).toBe("http://api.test/api/chat/stream");
  });

  it("shows the answer while it is being written, then completes it", async () => {
    const stream = controlledStream();
    fetchMock.mockResolvedValue(stream.response);
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "Count to three");
    expect(await screen.findByText("DotNextAI is thinking…")).toBeInTheDocument();

    stream.delta("one ");
    expect(await screen.findByText("one")).toBeInTheDocument();
    expect(screen.getByRole("log")).toHaveAttribute("aria-busy", "true");
    expect(screen.getByRole("button", { name: "Stop" })).toBeInTheDocument();

    stream.delta("two three");
    stream.done("one two three");

    expect(await screen.findByText("one two three")).toBeInTheDocument();
    expect(screen.getByText("gpt-5.6-luna")).toBeInTheDocument();
    expect(screen.getByRole("log")).not.toHaveAttribute("aria-busy");
    expect(screen.getByRole("button", { name: "Send" })).toBeInTheDocument();
  });

  it("stops the answer and keeps what had arrived", async () => {
    const stream = controlledStream();
    fetchMock.mockResolvedValue(stream.response);
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "Tell me a long story");
    stream.delta("Once upon a time");
    await screen.findByText("Once upon a time");

    await user.click(screen.getByRole("button", { name: "Stop" }));

    expect(screen.getByText("Stopped before the end.")).toBeInTheDocument();
    expect(within(screen.getByRole("log")).getByText("Tell me a long story")).toBeInTheDocument();
    expect((fetchMock.mock.calls[0][1] as RequestInit).signal?.aborted).toBe(true);
    expect(screen.getByRole("button", { name: "Send" })).toBeInTheDocument();
  });

  it("stopping before any text gives the question back", async () => {
    fetchMock.mockResolvedValue(controlledStream().response);
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "Hello?");
    await user.click(await screen.findByRole("button", { name: "Stop" }));

    expect(screen.getByRole("textbox", { name: "Your question" })).toHaveValue("Hello?");
    expect(screen.queryByRole("log")).not.toBeInTheDocument();
  });

  it("drops a partial answer that breaks and shows the failure", async () => {
    const stream = controlledStream();
    fetchMock.mockResolvedValue(stream.response);
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "Hi");
    stream.delta("Half an ");
    await screen.findByText("Half an");
    stream.error("Unavailable", 502);

    expect(await screen.findByText("The model provider is unavailable right now. Try again later.")).toBeInTheDocument();
    expect(screen.queryByText("Half an")).not.toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Your question" })).toHaveValue("Hi");
  });

  it("does not send an empty question", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await user.type(screen.getByRole("textbox", { name: "Your question" }), "   {Enter}");

    expect(fetchMock).not.toHaveBeenCalled();
    expect(await screen.findByText("Type a question first.")).toBeInTheDocument();
  });
});
