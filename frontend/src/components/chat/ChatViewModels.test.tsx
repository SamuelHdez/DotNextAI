import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ChatView } from "@/components/chat/ChatView";
import { initialModel, MODEL_STORAGE_KEY } from "@/lib/chat/modelPreference";
import { chatPageFetch, renderWithProviders, streamedAnswer } from "@/test/render";

let fetchMock: ReturnType<typeof vi.fn>;

beforeEach(() => {
  fetchMock = vi.fn().mockImplementation(() => Promise.resolve(streamedAnswer("ok")));
});

afterEach(() => {
  vi.unstubAllGlobals();
  window.localStorage.clear();
});

const sentBodies = () => fetchMock.mock.calls.map(([, init]) => JSON.parse((init as RequestInit).body as string));

async function send(user: ReturnType<typeof userEvent.setup>, text: string) {
  await user.type(screen.getByRole("textbox", { name: "Your question" }), text);
  await user.click(screen.getByRole("button", { name: "Send" }));
  await screen.findAllByText("ok");
}

describe("ChatView model selector", () => {
  it("shows the default model and sends no model for it", async () => {
    vi.stubGlobal("fetch", chatPageFetch(fetchMock));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    // The selector shows "Loading models…" until the list arrives.
    await waitFor(() => expect(screen.getByRole("combobox", { name: "Model" })).toHaveTextContent("gpt-5.6-luna (default)"));
    await send(user, "Hi");

    expect(sentBodies()[0]).not.toHaveProperty("model");
  });

  it("sends the chosen model, keeps the conversation and remembers the choice", async () => {
    vi.stubGlobal("fetch", chatPageFetch(fetchMock));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await send(user, "one");
    await user.click(await screen.findByRole("combobox", { name: "Model" }));
    await user.click(screen.getByRole("option", { name: "gpt-5.6-terra" }));
    await send(user, "two");

    expect(sentBodies()[1]).toEqual({ question: "two", model: "gpt-5.6-terra" });
    expect(window.localStorage.getItem(MODEL_STORAGE_KEY)).toBe("gpt-5.6-terra");
  });

  it("starts with the remembered model when the server still offers it", async () => {
    window.localStorage.setItem(MODEL_STORAGE_KEY, "gpt-5.6-terra");
    vi.stubGlobal("fetch", chatPageFetch(fetchMock));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await waitFor(() => expect(screen.getByRole("combobox", { name: "Model" })).toHaveTextContent("gpt-5.6-terra"));
    await send(user, "Hi");

    expect(sentBodies()[0].model).toBe("gpt-5.6-terra");
  });

  it("choosing the default again forgets the remembered model", async () => {
    window.localStorage.setItem(MODEL_STORAGE_KEY, "gpt-5.6-terra");
    vi.stubGlobal("fetch", chatPageFetch(fetchMock));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await waitFor(() => expect(screen.getByRole("combobox", { name: "Model" })).toHaveTextContent("gpt-5.6-terra"));
    await user.click(screen.getByRole("combobox", { name: "Model" }));
    await user.click(screen.getByRole("option", { name: "gpt-5.6-luna (default)" }));

    expect(window.localStorage.getItem(MODEL_STORAGE_KEY)).toBeNull();
  });

  it("falls back to the default when the list cannot be loaded", async () => {
    vi.stubGlobal("fetch", chatPageFetch(fetchMock, "fail"));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />, { locale: "es" });

    const picker = await screen.findByRole("combobox", { name: "Modelo" });
    await waitFor(() => expect(picker).toHaveTextContent("Modelo predeterminado"));
    expect(picker).toBeDisabled();

    await user.type(screen.getByRole("textbox", { name: "Tu pregunta" }), "Hola");
    await user.click(screen.getByRole("button", { name: "Enviar" }));
    await screen.findByText("ok");
    expect(sentBodies()[0]).not.toHaveProperty("model");
  });
});

describe("initialModel", () => {
  it("keeps a stored model only while the server offers it", () => {
    expect(initialModel(["a", "b"], "a", "b")).toBe("b");
    expect(initialModel(["a", "b"], "a", "gone")).toBe("a");
    expect(initialModel(["a", "b"], "a", null)).toBe("a");
  });
});
