import { describe, expect, it } from "vitest";
import { checkName, groupByFolder, sortFolders, titleFrom, toChatMessages, upsertConversation } from "@/lib/chat/savedChats";
import type { ConversationSummary, Folder } from "@/services/types";

function summary(id: string, updatedAt: string, folderId: string | null = null): ConversationSummary {
  const usage = { inputTokens: 0, outputTokens: 0, totalTokens: 0 };
  return { id, title: id, folderId, model: null, questionCount: 0, usage, createdAt: updatedAt, updatedAt };
}

const folder = (id: string, name: string): Folder => ({ id, name, conversationCount: 0, createdAt: "2026-11-16T10:00:00Z" });

describe("saved chats rules", () => {
  it.each([
    ["Plan the team offsite", "Plan the team offsite"],
    ["  first line\nsecond line", "first line"],
    ["many    spaces   here", "many spaces here"],
    ["A question that is clearly much longer than sixty characters to be a title", "A question that is clearly much longer than sixty characters…"],
  ])("titles %j as the server does", (question, title) => {
    expect(titleFrom(question)).toBe(title);
  });

  it("checks names like the server", () => {
    expect(checkName("  Work ", 100)).toEqual({ ok: true, value: "Work" });
    expect(checkName("   ", 100)).toMatchObject({ ok: false, reason: "empty" });
    expect(checkName("a".repeat(101), 100)).toMatchObject({ ok: false, reason: "tooLong", max: 100 });
    expect(checkName("tab\there", 100)).toMatchObject({ ok: false, reason: "control" });
  });

  it("keeps the most recently updated conversation first", () => {
    const list = [summary("b", "2026-11-16T10:02:00Z"), summary("a", "2026-11-16T10:01:00Z")];

    expect(upsertConversation(list, summary("a", "2026-11-16T10:03:00Z")).map((c) => c.id)).toEqual(["a", "b"]);
    expect(upsertConversation(list, summary("c", "2026-11-16T10:00:00Z")).map((c) => c.id)).toEqual(["b", "a", "c"]);
  });

  it("groups conversations by folder; an unknown folder counts as none", () => {
    const groups = groupByFolder(sortFolders([folder("w", "work"), folder("a", "Archive")]), [
      summary("1", "x", "w"),
      summary("2", "x"),
      summary("3", "x", "gone"),
    ]);

    expect(groups.folders.map(({ folder: f, conversations }) => [f.name, conversations.map((c) => c.id)])).toEqual([
      ["Archive", []],
      ["work", ["1"]],
    ]);
    expect(groups.unfiled.map((c) => c.id)).toEqual(["2", "3"]);
  });

  it("maps saved messages to the chat's, with absolute image URLs", () => {
    const messages = toChatMessages(
      {
        conversation: summary("c", "x"),
        messages: [
          { id: "q", role: "user", text: "What?", images: [{ id: "i", mediaType: "image/png", size: 3, url: "/api/conversations/c/images/i" }], model: null, usage: null, truncated: false, stopped: false, createdAt: "x" },
          { id: "a", role: "assistant", text: "Half", images: [], model: "m", usage: null, truncated: false, stopped: true, createdAt: "x" },
        ],
      },
      (url) => `http://api.test${url}`,
    );

    expect(messages).toEqual([
      { id: "q", role: "user", text: "What?", images: [{ name: "1", src: "http://api.test/api/conversations/c/images/i" }] },
      { id: "a", role: "assistant", text: "Half", model: "m", stopped: true },
    ]);
  });
});
