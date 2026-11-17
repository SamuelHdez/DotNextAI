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

  it("shows the cost of each answer and of the conversation", async () => {
    const chat = vi
      .fn()
      .mockResolvedValueOnce(streamedAnswer("first", false, "gpt-6-luna", { inputTokens: 1200, outputTokens: 34, totalTokens: 1234, cost: 0.000137 }))
      .mockResolvedValueOnce(streamedAnswer("second", false, "gpt-6-luna", { inputTokens: 1300, outputTokens: 66, totalTokens: 1366, cost: 0.000163 }));
    vi.stubGlobal("fetch", chatPageFetch(chat));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "one", "first");
    expect(within(screen.getByRole("log")).getByText("$0.00014")).toBeInTheDocument();
    expect(screen.getByText("Cost: $0.00014, at the LiteLLM proxy's prices.")).toBeInTheDocument();

    await ask(user, "two", "second");
    expect(within(screen.getByRole("log")).getByText("$0.00016")).toBeInTheDocument();
    expect(screen.getByText("Cost: $0.00030, at the LiteLLM proxy's prices.")).toBeInTheDocument();
  });

  it("shows the cost in the selected language", async () => {
    vi.stubGlobal("fetch", chatPageFetch(vi.fn().mockResolvedValue(streamedAnswer("¡Hola!", false, "gpt-6-luna", { inputTokens: 1200, outputTokens: 34, totalTokens: 1234, cost: 0.000137 }))));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />, { locale: "es" });

    await ask(user, "hola", "¡Hola!");

    expect(within(screen.getByRole("log")).getByText("0,00014 US$")).toBeInTheDocument();
    expect(screen.getByText("Coste: 0,00014 US$, con los precios del proxy LiteLLM.")).toBeInTheDocument();
  });

  it("shows the tokens without a cost when the model has no price", async () => {
    vi.stubGlobal("fetch", chatPageFetch(vi.fn().mockResolvedValue(streamedAnswer("ok", false, "gpt-6-luna", { inputTokens: 10, outputTokens: 2, totalTokens: 12, cost: null }))));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await ask(user, "Hi", "ok");

    expect(screen.getByText("This conversation: 12 tokens (10 in · 2 out) over 1 answers.")).toBeInTheDocument();
    expect(screen.queryByText(/Cost:/)).not.toBeInTheDocument();
    expect(screen.queryByText(/\$/)).not.toBeInTheDocument();
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
    ).toEqual({ inputTokens: 30, outputTokens: 5, totalTokens: 35, cost: null, answers: 2 });
    expect(conversationUsage([])).toBeNull();
  });

  it("sums the costs, and the total cost is unknown once an answer has none", () => {
    const priced = { id: "2", role: "assistant" as const, text: "b", usage: { inputTokens: 10, outputTokens: 2, totalTokens: 12, cost: 0.0001 } };
    expect(conversationUsage([priced, { ...priced, id: "4", usage: { ...priced.usage, cost: 0.0002 } }])?.cost).toBeCloseTo(0.0003, 10);
    expect(conversationUsage([priced, { ...priced, id: "4", usage: { inputTokens: 1, outputTokens: 1, totalTokens: 2, cost: null } }])?.cost).toBeNull();
  });
});
