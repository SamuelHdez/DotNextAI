import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ChatView } from "@/components/chat/ChatView";
import { conversationUsage } from "@/lib/chat/conversation";
import { chatPageFetch, renderWithProviders, streamedAnswer } from "@/test/render";

afterEach(() => vi.unstubAllGlobals());

async function ask(user: ReturnType<typeof userEvent.setup>, text: string, answer: string) {
  await user.type(screen.getByRole("textbox", { name: /question|pregunta/i }), text);
  await user.keyboard("{Enter}");
  await screen.findByText(answer);
}

describe("ChatView token metrics", () => {
  it("shows each answer's tokens and the conversation total", async () => {
    const chat = vi
      .fn()
      .mockResolvedValueOnce(streamedAnswer("first", false, "gpt-5.6-luna", { inputTokens: 1200, outputTokens: 34, totalTokens: 1234 }))
      .mockResolvedValueOnce(streamedAnswer("second", false, "gpt-5.6-luna", { inputTokens: 1300, outputTokens: 66, totalTokens: 1366 }));
    vi.stubGlobal("fetch", chatPageFetch(chat));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "one", "first");
    expect(within(screen.getByRole("log")).getByText("1,200 in · 34 out tokens")).toBeInTheDocument();
    expect(screen.getByText("This conversation: 1,234 tokens (1,200 in · 34 out) over 1 answers.")).toBeInTheDocument();

    await ask(user, "two", "second");
    expect(screen.getByText("This conversation: 2,600 tokens (2,500 in · 100 out) over 2 answers.")).toBeInTheDocument();
    expect(screen.getByText("Every question resends the conversation, so input tokens grow with each turn.")).toBeInTheDocument();
  });

  it("shows the numbers in the selected language", async () => {
    vi.stubGlobal("fetch", chatPageFetch(vi.fn().mockResolvedValue(streamedAnswer("¡Hola!", false, "gpt-5.6-luna", { inputTokens: 1200, outputTokens: 34, totalTokens: 1234 }))));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />, { locale: "es" });

    await ask(user, "hola", "¡Hola!");

    expect(screen.getByText("1200 de entrada · 34 de salida")).toBeInTheDocument();
    expect(screen.getByText("Esta conversación: 1234 tokens (1200 de entrada · 34 de salida) en 1 respuestas.")).toBeInTheDocument();
  });

  it("shows no totals when the proxy reports no usage", async () => {
    vi.stubGlobal("fetch", chatPageFetch(vi.fn().mockResolvedValue(streamedAnswer("ok"))));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "Hi", "ok");

    expect(screen.queryByText(/This conversation:/)).not.toBeInTheDocument();
    expect(screen.queryByText(/tokens/)).not.toBeInTheDocument();
  });

  it("forgets the total with a new conversation", async () => {
    vi.stubGlobal("fetch", chatPageFetch(vi.fn().mockResolvedValue(streamedAnswer("ok", false, "gpt-5.6-luna", { inputTokens: 10, outputTokens: 2, totalTokens: 12 }))));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "Hi", "ok");
    await user.click(screen.getByRole("button", { name: "New conversation" }));

    expect(screen.queryByText(/This conversation:/)).not.toBeInTheDocument();
  });
});

describe("conversationUsage", () => {
  it("sums only the answers with a known usage", () => {
    expect(
      conversationUsage([
        { id: "1", role: "user", text: "a" },
        { id: "2", role: "assistant", text: "b", usage: { inputTokens: 10, outputTokens: 2, totalTokens: 12 } },
        { id: "3", role: "assistant", text: "c", stopped: true },
        { id: "4", role: "assistant", text: "d", usage: { inputTokens: 20, outputTokens: 3, totalTokens: 23 } },
      ]),
    ).toEqual({ inputTokens: 30, outputTokens: 5, totalTokens: 35, answers: 2 });
    expect(conversationUsage([])).toBeNull();
  });
});
