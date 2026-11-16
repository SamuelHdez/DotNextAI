import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ChatView } from "@/components/chat/ChatView";
import { jsonResponse, renderWithProviders } from "@/test/render";

// A limit of one question keeps the test short; the real value (50) is checked in chat.test.ts.
vi.mock("@/lib/chat/chatRules", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/chat/chatRules")>()),
  MAX_CONVERSATION_QUESTIONS: 1,
}));

afterEach(() => vi.unstubAllGlobals());

describe("ChatView at the question limit", () => {
  it("asks for a new conversation and disables the composer", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse({ answer: "ok", model: "gpt-5.6-luna", truncated: false })));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await user.type(screen.getByRole("textbox", { name: "Your question" }), "one{Enter}");

    expect(await screen.findByText(/reached the limit of 1 questions/)).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Your question" })).toBeDisabled();

    await user.click(screen.getByRole("button", { name: "New conversation" }));
    expect(screen.getByRole("textbox", { name: "Your question" })).toBeEnabled();
  });
});
