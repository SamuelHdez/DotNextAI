import { describe, expect, it } from "vitest";
import { chatErrorMessage } from "@/lib/chat/chatErrors";
import { checkQuestion, MAX_QUESTION_LENGTH } from "@/lib/chat/chatRules";
import { questionCount, toHistory, type ChatMessage } from "@/lib/chat/conversation";
import { ApiError } from "@/services/apiClient";
import { CHAT_FAILURES } from "@/services/types";

describe("checkQuestion", () => {
  it("trims a valid question", () => {
    expect(checkQuestion("  Hi \n")).toEqual({ ok: true, question: "Hi" });
  });

  it.each(["", "   ", "\n\t"])("rejects an empty question (%j)", (text) => {
    expect(checkQuestion(text)).toEqual({ ok: false, reason: "empty" });
  });

  it("accepts the maximum length and rejects one more character", () => {
    expect(checkQuestion("a".repeat(MAX_QUESTION_LENGTH)).ok).toBe(true);
    expect(checkQuestion("a".repeat(MAX_QUESTION_LENGTH + 1))).toEqual({ ok: false, reason: "tooLong", length: MAX_QUESTION_LENGTH + 1 });
  });
});

describe("conversation", () => {
  const messages: ChatMessage[] = [
    { id: "1", role: "user", text: "My name is Ana." },
    { id: "2", role: "assistant", text: "Hi Ana!", model: "gpt-5.6-luna", truncated: false },
  ];

  it("sends only roles and texts as history", () => {
    expect(toHistory(messages)).toEqual([
      { role: "user", text: "My name is Ana." },
      { role: "assistant", text: "Hi Ana!" },
    ]);
  });

  it("counts the questions", () => {
    expect(questionCount(messages)).toBe(1);
    expect(questionCount([])).toBe(0);
  });
});

describe("chatErrorMessage", () => {
  it.each(CHAT_FAILURES)("maps the %s failure to its own text", (failure) => {
    expect(chatErrorMessage(new ApiError("x", 503, { failure }))).toEqual({ key: `error.${failure}` });
  });

  it("maps a network error", () => {
    expect(chatErrorMessage(new ApiError("x", 0))).toEqual({ key: "error.network" });
    expect(chatErrorMessage(new TypeError("Failed to fetch"))).toEqual({ key: "error.network" });
  });

  it("maps a validation problem without failure", () => {
    expect(chatErrorMessage(new ApiError("x", 400, { errors: { question: ["The question is empty."] } }))).toEqual({ key: "error.validation" });
  });

  it("falls back to the status for unknown problems", () => {
    expect(chatErrorMessage(new ApiError("x", 500, { failure: "SomethingNew" }))).toEqual({ key: "error.unknown", params: { status: 500 } });
  });
});
